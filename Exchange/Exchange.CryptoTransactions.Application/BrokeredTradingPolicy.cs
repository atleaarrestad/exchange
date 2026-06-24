namespace Exchange.CryptoTransactions.Application;

public sealed record BrokeredTradingPolicy
{
    public int QuoteTtlSeconds { get; init; } = 15;
    public decimal InternalOnlySpreadBasisPoints { get; init; } = 35m;
    public decimal ExternalHedgeSpreadBasisPoints { get; init; } = 90m;
    public decimal MaxAllowedSlippageBasisPoints { get; init; } = 200m;
    public int MaxBufferedHedgeCustomerBuys { get; init; } = 10;
    public int MaxBufferedHedgeDelaySeconds { get; init; } = 30;

    public TimeSpan QuoteTtl => TimeSpan.FromSeconds(QuoteTtlSeconds);
    public TimeSpan MaxBufferedHedgeDelay => TimeSpan.FromSeconds(MaxBufferedHedgeDelaySeconds);

    public void Validate()
    {
        if (QuoteTtlSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(QuoteTtlSeconds), QuoteTtlSeconds, "QuoteTtlSeconds must be greater than zero.");
        }

        if (InternalOnlySpreadBasisPoints < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(InternalOnlySpreadBasisPoints), InternalOnlySpreadBasisPoints, "InternalOnlySpreadBasisPoints cannot be negative.");
        }

        if (ExternalHedgeSpreadBasisPoints < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(ExternalHedgeSpreadBasisPoints), ExternalHedgeSpreadBasisPoints, "ExternalHedgeSpreadBasisPoints cannot be negative.");
        }

        if (MaxAllowedSlippageBasisPoints < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAllowedSlippageBasisPoints), MaxAllowedSlippageBasisPoints, "MaxAllowedSlippageBasisPoints cannot be negative.");
        }

        if (MaxBufferedHedgeCustomerBuys <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxBufferedHedgeCustomerBuys), MaxBufferedHedgeCustomerBuys, "MaxBufferedHedgeCustomerBuys must be greater than zero.");
        }

        if (MaxBufferedHedgeDelaySeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxBufferedHedgeDelaySeconds), MaxBufferedHedgeDelaySeconds, "MaxBufferedHedgeDelaySeconds must be greater than zero.");
        }
    }
}
