using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Exchange.CryptoTransactions.Application;

public sealed class CryptoTransferService(
    ICryptoTransferFundsReservationGateway fundsReservationGateway,
    ICryptoTransferIdempotencyStore idempotencyStore,
    ISubmitCryptoTransferCommandValidator commandValidator) : ICryptoTransferService
{
    public async Task<CryptoTransferReceipt> SubmitAsync(SubmitCryptoTransferCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        commandValidator.Validate(command);

        var sourceAccountId = command.SourceAccountId.Trim();
        var assetSymbol = AssetSymbol.Parse(command.AssetSymbol, nameof(command.AssetSymbol));
        var idempotencyKey = command.IdempotencyKey.Trim();
        var destinationAddress = command.DestinationAddress.Trim();
        var requestFingerprint = CreateRequestFingerprint(
            idempotencyKey,
            sourceAccountId,
            destinationAddress,
            assetSymbol,
            command.Amount,
            command.NetworkFee);
        var totalDebit = checked(command.Amount + command.NetworkFee);
        var registration = await idempotencyStore.RegisterPendingAsync(
            sourceAccountId,
            assetSymbol,
            idempotencyKey,
            requestFingerprint,
            totalDebit,
            destinationAddress,
            command.Amount,
            command.NetworkFee,
            cancellationToken);
        if (registration.CompletedReceipt is not null)
        {
            return registration.CompletedReceipt;
        }

        if (registration.CreatedPending)
        {
            var operation = new PendingCryptoTransferOperation(
                sourceAccountId,
                assetSymbol,
                idempotencyKey,
                requestFingerprint,
                totalDebit,
                destinationAddress,
                command.Amount,
                command.NetworkFee,
                DateTimeOffset.MinValue,
                DateTimeOffset.MinValue);

            try
            {
                await fundsReservationGateway.ReserveAsync(
                    sourceAccountId,
                    assetSymbol,
                    totalDebit,
                    idempotencyKey,
                    CancellationToken.None);
            }
            catch (Exception exception) when (
                exception is InsufficientFundsException
                or ApplicationValidationException
                or ArgumentException
                or ExternalDependencyNotConfiguredException)
            {
                var released = await idempotencyStore.TryReleasePendingAsync(operation, CancellationToken.None);
                if (!released)
                {
                    throw new InvalidOperationException(
                        $"Unable to release pending transfer '{sourceAccountId}/{assetSymbol.Value}/{idempotencyKey}' after reservation failure.",
                        exception);
                }

                throw;
            }
        }

        return CreatePendingReceipt(sourceAccountId, assetSymbol, idempotencyKey, totalDebit);
    }

    private static CryptoTransferReceipt CreatePendingReceipt(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        decimal totalDebit)
    {
        return new CryptoTransferReceipt(
            TransferId: DeriveDeterministicTransferId(sourceAccountId, assetSymbol, idempotencyKey),
            GatewayTransactionId: string.Empty,
            SubmittedAtUtc: DateTimeOffset.UtcNow,
            TotalDebit: totalDebit,
            RequiredConfirmations: 0,
            Status: CryptoTransferReceiptStatus.Pending);
    }

    private static Guid DeriveDeterministicTransferId(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey)
    {
        var payload = $"{sourceAccountId}|{assetSymbol.Value}|{idempotencyKey}";
        Span<byte> hash = stackalloc byte[32];
        SHA256.TryHashData(Encoding.UTF8.GetBytes(payload), hash, out _);
        return new Guid(hash[..16]);
    }

    private static string CreateRequestFingerprint(
        string idempotencyKey,
        string sourceAccountId,
        string destinationAddress,
        AssetSymbol assetSymbol,
        decimal amount,
        decimal networkFee)
    {
        var payload = string.Join(
            '|',
            idempotencyKey,
            sourceAccountId,
            destinationAddress,
            assetSymbol.Value,
            amount.ToString("G29", CultureInfo.InvariantCulture),
            networkFee.ToString("G29", CultureInfo.InvariantCulture));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
