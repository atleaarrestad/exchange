namespace Exchange.CryptoTransactions.Application.Validation;

public sealed class CryptoGatewayResilienceSettingsCommandValidator : ICryptoGatewayResilienceSettingsCommandValidator
{
    public void Validate(CreateCryptoGatewayResilienceSettingsProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCore(
            command.Name,
            command.OperationTimeoutSeconds,
            command.RetryCount,
            command.RetryDelayMilliseconds,
            command.FailureRatio,
            command.MinimumThroughput,
            command.SamplingDurationSeconds,
            command.BreakDurationSeconds,
            command.MaxParallelization,
            command.MaxQueueingActions);
    }

    public void Validate(UpdateCryptoGatewayResilienceSettingsProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCore(
            command.Name,
            command.OperationTimeoutSeconds,
            command.RetryCount,
            command.RetryDelayMilliseconds,
            command.FailureRatio,
            command.MinimumThroughput,
            command.SamplingDurationSeconds,
            command.BreakDurationSeconds,
            command.MaxParallelization,
            command.MaxQueueingActions);
    }

    private static void ValidateCore(
        string name,
        int operationTimeoutSeconds,
        int retryCount,
        int retryDelayMilliseconds,
        double failureRatio,
        int minimumThroughput,
        int samplingDurationSeconds,
        int breakDurationSeconds,
        int maxParallelization,
        int maxQueueingActions)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(name))
        {
            errors[nameof(name)] = ["Name is required."];
        }

        if (operationTimeoutSeconds <= 0)
        {
            errors[nameof(operationTimeoutSeconds)] = ["OperationTimeoutSeconds must be greater than zero."];
        }

        if (retryCount < 0)
        {
            errors[nameof(retryCount)] = ["RetryCount cannot be negative."];
        }

        if (retryDelayMilliseconds < 0)
        {
            errors[nameof(retryDelayMilliseconds)] = ["RetryDelayMilliseconds cannot be negative."];
        }

        if (failureRatio <= 0d || failureRatio >= 1d)
        {
            errors[nameof(failureRatio)] = ["FailureRatio must be greater than 0 and less than 1."];
        }

        if (minimumThroughput <= 1)
        {
            errors[nameof(minimumThroughput)] = ["MinimumThroughput must be greater than one."];
        }

        if (samplingDurationSeconds <= 0)
        {
            errors[nameof(samplingDurationSeconds)] = ["SamplingDurationSeconds must be greater than zero."];
        }

        if (breakDurationSeconds <= 0)
        {
            errors[nameof(breakDurationSeconds)] = ["BreakDurationSeconds must be greater than zero."];
        }

        if (maxParallelization <= 0)
        {
            errors[nameof(maxParallelization)] = ["MaxParallelization must be greater than zero."];
        }

        if (maxQueueingActions < 0)
        {
            errors[nameof(maxQueueingActions)] = ["MaxQueueingActions cannot be negative."];
        }

        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }
    }
}
