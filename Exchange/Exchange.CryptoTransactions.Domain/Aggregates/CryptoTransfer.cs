using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Domain.Aggregates;

public sealed class CryptoTransfer
{
    public Guid Id { get; }
    public string IdempotencyKey { get; }
    public string SourceAccountId { get; }
    public string DestinationAddress { get; }
    public CryptoAmount Amount { get; }
    public NetworkFee Fee { get; }
    public decimal TotalDebit { get; }
    public DateTimeOffset CreatedAtUtc { get; }

    public CryptoTransfer(
        string idempotencyKey,
        string sourceAccountId,
        string destinationAddress,
        CryptoAmount amount,
        NetworkFee fee,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationAddress);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(fee);

        Id = Guid.CreateVersion7();
        IdempotencyKey = idempotencyKey.Trim();
        SourceAccountId = sourceAccountId.Trim();
        DestinationAddress = destinationAddress.Trim();
        Amount = amount;
        Fee = fee;
        if (fee.Value > amount.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(fee), fee.Value, "Network fee cannot exceed transfer amount.");
        }

        TotalDebit = checked(amount.Value + fee.Value);
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }
}
