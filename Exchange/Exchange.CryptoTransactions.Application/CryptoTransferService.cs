using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Domain.Aggregates;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Polly;
using Polly.Timeout;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Exchange.CryptoTransactions.Application;

public sealed class CryptoTransferService(
    IBlockchainTransferGateway blockchainTransferGateway,
    ICryptoTransferFundsReservationGateway fundsReservationGateway,
    ICryptoTransferIdempotencyStore idempotencyStore,
    ISubmitCryptoTransferCommandValidator commandValidator) : ICryptoTransferService
{
    private static readonly TimeSpan GatewaySubmitTimeout = TimeSpan.FromSeconds(30);
    private static readonly AsyncTimeoutPolicy<BlockchainTransferResult> GatewayTimeoutPolicy =
        Policy.TimeoutAsync<BlockchainTransferResult>(GatewaySubmitTimeout, TimeoutStrategy.Optimistic);

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

        return await idempotencyStore.ExecuteOnceAsync(
            sourceAccountId,
            assetSymbol,
            idempotencyKey,
            requestFingerprint,
            totalDebit,
            async operationCancellationToken =>
            {
                var transfer = new CryptoTransfer(
                    idempotencyKey,
                    sourceAccountId,
                    destinationAddress,
                    new CryptoAmount(assetSymbol, command.Amount),
                    new NetworkFee(command.NetworkFee),
                    DateTimeOffset.UtcNow);

                var gatewayRequest = new BlockchainTransferRequest(
                    transfer.IdempotencyKey,
                    transfer.SourceAccountId,
                    transfer.DestinationAddress,
                    transfer.Amount.AssetSymbol,
                    transfer.Amount.Value,
                    transfer.Fee.Value,
                    transfer.TotalDebit);

                await fundsReservationGateway.ReserveAsync(
                    transfer.SourceAccountId,
                    transfer.Amount.AssetSymbol,
                    transfer.TotalDebit,
                    transfer.IdempotencyKey,
                    operationCancellationToken);

                try
                {
                    var gatewayResult = await GatewayTimeoutPolicy.ExecuteAsync(
                        async token => await blockchainTransferGateway.SubmitAsync(gatewayRequest, token),
                        operationCancellationToken);

                    await fundsReservationGateway.CommitAsync(
                        transfer.SourceAccountId,
                        transfer.Amount.AssetSymbol,
                        transfer.IdempotencyKey,
                        operationCancellationToken);

                    return new CryptoTransferReceipt(
                        transfer.Id,
                        gatewayResult.GatewayTransactionId,
                        gatewayResult.SubmittedAtUtc,
                        transfer.TotalDebit,
                        gatewayResult.RequiredConfirmations);
                }
                catch (TimeoutRejectedException exception)
                {
                    throw new BlockchainTransferTimeoutException(
                        $"Blockchain gateway did not respond within {GatewaySubmitTimeout.TotalSeconds:0} seconds.",
                        exception);
                }
                catch (BlockchainTransferRejectedException)
                {
                    await fundsReservationGateway.ReleaseAsync(
                        transfer.SourceAccountId,
                        transfer.Amount.AssetSymbol,
                        transfer.IdempotencyKey,
                        operationCancellationToken);
                    throw;
                }
                catch (ExternalDependencyNotConfiguredException)
                {
                    await fundsReservationGateway.ReleaseAsync(
                        transfer.SourceAccountId,
                        transfer.Amount.AssetSymbol,
                        transfer.IdempotencyKey,
                        operationCancellationToken);
                    throw;
                }
            },
            cancellationToken);
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
