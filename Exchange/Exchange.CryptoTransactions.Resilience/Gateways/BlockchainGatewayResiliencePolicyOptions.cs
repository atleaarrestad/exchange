namespace Exchange.CryptoTransactions.Resilience.Gateways;

public sealed record BlockchainGatewayResiliencePolicyOptions
{
    public bool Enabled { get; init; } = true;
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(20);
    public double FailureRatio { get; init; } = 0.5;
    public int MinimumThroughput { get; init; } = 20;
    public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxParallelization { get; init; } = 32;
    public int MaxQueueingActions { get; init; } = 64;

    public static void Validate(BlockchainGatewayResiliencePolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.OperationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.OperationTimeout), options.OperationTimeout, "OperationTimeout must be greater than zero.");
        }

        if (options.FailureRatio <= 0d || options.FailureRatio >= 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(options.FailureRatio), options.FailureRatio, "FailureRatio must be greater than 0 and less than 1.");
        }

        if (options.MinimumThroughput <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MinimumThroughput), options.MinimumThroughput, "MinimumThroughput must be greater than one.");
        }

        if (options.SamplingDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.SamplingDuration), options.SamplingDuration, "SamplingDuration must be greater than zero.");
        }

        if (options.BreakDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.BreakDuration), options.BreakDuration, "BreakDuration must be greater than zero.");
        }

        if (options.MaxParallelization <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxParallelization), options.MaxParallelization, "MaxParallelization must be greater than zero.");
        }

        if (options.MaxQueueingActions < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxQueueingActions), options.MaxQueueingActions, "MaxQueueingActions cannot be negative.");
        }
    }
}
