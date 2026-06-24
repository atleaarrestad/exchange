namespace Exchange.CryptoTransactions.Application.Validation;

public sealed class CryptoSettingsCommandValidator : ICryptoSettingsCommandValidator
{
    public void Validate(CreateCryptoSettingsProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCore(
            command.Name,
            command.QuoteTtlSeconds,
            command.InternalOnlySpreadBasisPoints,
            command.ExternalHedgeSpreadBasisPoints,
            command.MaxAllowedSlippageBasisPoints,
            command.BitcoinReferencePriceNok,
            command.EtherReferencePriceNok,
            command.InitialBitcoinInventory,
            command.InitialEtherInventory,
            command.MaxBufferedHedgeCustomerBuys,
            command.MaxBufferedHedgeDelaySeconds,
            command.TimeoutReconciliationScanIntervalSeconds,
            command.TimeoutReconciliationStaleAfterSeconds,
            command.SimulationMinLatencyMs,
            command.SimulationMaxLatencyMs,
            command.SimulationRejectRate,
            command.SimulationTimeoutRate,
            command.SimulationDefaultBitcoinAvailableBalance,
            command.SimulationDefaultEtherAvailableBalance);
    }

    public void Validate(UpdateCryptoSettingsProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCore(
            command.Name,
            command.QuoteTtlSeconds,
            command.InternalOnlySpreadBasisPoints,
            command.ExternalHedgeSpreadBasisPoints,
            command.MaxAllowedSlippageBasisPoints,
            command.BitcoinReferencePriceNok,
            command.EtherReferencePriceNok,
            command.InitialBitcoinInventory,
            command.InitialEtherInventory,
            command.MaxBufferedHedgeCustomerBuys,
            command.MaxBufferedHedgeDelaySeconds,
            command.TimeoutReconciliationScanIntervalSeconds,
            command.TimeoutReconciliationStaleAfterSeconds,
            command.SimulationMinLatencyMs,
            command.SimulationMaxLatencyMs,
            command.SimulationRejectRate,
            command.SimulationTimeoutRate,
            command.SimulationDefaultBitcoinAvailableBalance,
            command.SimulationDefaultEtherAvailableBalance);
    }

    private static void ValidateCore(
        string name,
        int quoteTtlSeconds,
        decimal internalOnlySpreadBasisPoints,
        decimal externalHedgeSpreadBasisPoints,
        decimal maxAllowedSlippageBasisPoints,
        decimal bitcoinReferencePriceNok,
        decimal etherReferencePriceNok,
        decimal initialBitcoinInventory,
        decimal initialEtherInventory,
        int maxBufferedHedgeCustomerBuys,
        int maxBufferedHedgeDelaySeconds,
        int timeoutReconciliationScanIntervalSeconds,
        int timeoutReconciliationStaleAfterSeconds,
        int simulationMinLatencyMs,
        int simulationMaxLatencyMs,
        decimal simulationRejectRate,
        decimal simulationTimeoutRate,
        decimal simulationDefaultBitcoinAvailableBalance,
        decimal simulationDefaultEtherAvailableBalance)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(name))
        {
            errors[nameof(name)] = ["Name is required."];
        }

        if (quoteTtlSeconds <= 0)
        {
            errors[nameof(quoteTtlSeconds)] = ["QuoteTtlSeconds must be greater than zero."];
        }

        if (internalOnlySpreadBasisPoints < 0m)
        {
            errors[nameof(internalOnlySpreadBasisPoints)] = ["InternalOnlySpreadBasisPoints cannot be negative."];
        }

        if (externalHedgeSpreadBasisPoints < 0m)
        {
            errors[nameof(externalHedgeSpreadBasisPoints)] = ["ExternalHedgeSpreadBasisPoints cannot be negative."];
        }

        if (maxAllowedSlippageBasisPoints < 0m)
        {
            errors[nameof(maxAllowedSlippageBasisPoints)] = ["MaxAllowedSlippageBasisPoints cannot be negative."];
        }

        if (bitcoinReferencePriceNok <= 0m)
        {
            errors[nameof(bitcoinReferencePriceNok)] = ["BitcoinReferencePriceNok must be greater than zero."];
        }

        if (etherReferencePriceNok <= 0m)
        {
            errors[nameof(etherReferencePriceNok)] = ["EtherReferencePriceNok must be greater than zero."];
        }

        if (initialBitcoinInventory < 0m)
        {
            errors[nameof(initialBitcoinInventory)] = ["InitialBitcoinInventory cannot be negative."];
        }

        if (initialEtherInventory < 0m)
        {
            errors[nameof(initialEtherInventory)] = ["InitialEtherInventory cannot be negative."];
        }

        if (maxBufferedHedgeCustomerBuys <= 0)
        {
            errors[nameof(maxBufferedHedgeCustomerBuys)] = ["MaxBufferedHedgeCustomerBuys must be greater than zero."];
        }

        if (maxBufferedHedgeDelaySeconds <= 0)
        {
            errors[nameof(maxBufferedHedgeDelaySeconds)] = ["MaxBufferedHedgeDelaySeconds must be greater than zero."];
        }

        if (timeoutReconciliationScanIntervalSeconds <= 0)
        {
            errors[nameof(timeoutReconciliationScanIntervalSeconds)] = ["TimeoutReconciliationScanIntervalSeconds must be greater than zero."];
        }

        if (timeoutReconciliationStaleAfterSeconds <= 0)
        {
            errors[nameof(timeoutReconciliationStaleAfterSeconds)] = ["TimeoutReconciliationStaleAfterSeconds must be greater than zero."];
        }

        if (simulationMinLatencyMs < 0)
        {
            errors[nameof(simulationMinLatencyMs)] = ["SimulationMinLatencyMs cannot be negative."];
        }

        if (simulationMaxLatencyMs < simulationMinLatencyMs)
        {
            errors[nameof(simulationMaxLatencyMs)] = ["SimulationMaxLatencyMs must be greater than or equal to SimulationMinLatencyMs."];
        }

        if (simulationRejectRate is < 0m or > 1m)
        {
            errors[nameof(simulationRejectRate)] = ["SimulationRejectRate must be between 0 and 1."];
        }

        if (simulationTimeoutRate is < 0m or > 1m)
        {
            errors[nameof(simulationTimeoutRate)] = ["SimulationTimeoutRate must be between 0 and 1."];
        }

        if (simulationRejectRate + simulationTimeoutRate > 1m)
        {
            errors[nameof(simulationTimeoutRate)] = ["SimulationRejectRate + SimulationTimeoutRate cannot exceed 1."];
        }

        if (simulationDefaultBitcoinAvailableBalance < 0m)
        {
            errors[nameof(simulationDefaultBitcoinAvailableBalance)] = ["SimulationDefaultBitcoinAvailableBalance cannot be negative."];
        }

        if (simulationDefaultEtherAvailableBalance < 0m)
        {
            errors[nameof(simulationDefaultEtherAvailableBalance)] = ["SimulationDefaultEtherAvailableBalance cannot be negative."];
        }

        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }
    }
}
