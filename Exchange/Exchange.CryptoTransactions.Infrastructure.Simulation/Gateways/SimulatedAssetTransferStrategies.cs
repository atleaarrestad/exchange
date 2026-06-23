using Exchange.CryptoTransactions.Application.Contracts;
using DomainAssetSymbol = Exchange.CryptoTransactions.Domain.ValueObjects.AssetSymbol;
using System.Text.RegularExpressions;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

public sealed class SimulatedBitcoinTransferStrategy(
    SimulatedBlockchainTransferGatewayOptions options) : IBlockchainTransferStrategy
{
    private const decimal MinFee = 0.000001m;
    private const decimal MaxFee = 0.1m;
    private const int RequiredConfirmations = 3;
    private const string TransactionPrefix = "btc";
    private static readonly Regex AddressPattern = new("^(bc1|[13])[A-Za-z0-9]{20,90}$", RegexOptions.Compiled);

    public DomainAssetSymbol AssetSymbol => DomainAssetSymbol.Bitcoin;

    public async Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SimulatedTransferRules.ValidateAddress(AddressPattern, request.DestinationAddress, "Bitcoin");
        SimulatedTransferRules.ValidateFee(request.NetworkFee, MinFee, MaxFee, "Bitcoin");

        await SimulatedGatewayBehavior.SimulateGatewayDelayAndFailureAsync(options, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        var transactionId = $"{TransactionPrefix}-{Guid.CreateVersion7():N}";
        return new BlockchainTransferResult(transactionId, DateTimeOffset.UtcNow, RequiredConfirmations);
    }
}

public sealed class SimulatedEtherTransferStrategy(
    SimulatedBlockchainTransferGatewayOptions options) : IBlockchainTransferStrategy
{
    private const decimal MinFee = 0.000000001m;
    private const decimal MaxFee = 1m;
    private const int RequiredConfirmations = 12;
    private const string TransactionPrefix = "eth";
    private static readonly Regex AddressPattern = new("^0x[0-9a-fA-F]{40}$", RegexOptions.Compiled);

    public DomainAssetSymbol AssetSymbol => DomainAssetSymbol.Ether;

    public async Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        SimulatedTransferRules.ValidateAddress(AddressPattern, request.DestinationAddress, "Ether");
        SimulatedTransferRules.ValidateFee(request.NetworkFee, MinFee, MaxFee, "Ether");

        await SimulatedGatewayBehavior.SimulateGatewayDelayAndFailureAsync(options, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        var transactionId = $"{TransactionPrefix}-{Guid.CreateVersion7():N}";
        return new BlockchainTransferResult(transactionId, DateTimeOffset.UtcNow, RequiredConfirmations);
    }
}

file static class SimulatedGatewayBehavior
{
    public static async Task SimulateGatewayDelayAndFailureAsync(SimulatedBlockchainTransferGatewayOptions options, CancellationToken cancellationToken)
    {
        var delayMs = Random.Shared.Next(options.MinLatencyMs, options.MaxLatencyMs + 1);
        await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);

        var randomValue = Random.Shared.Next(0, 10_000) / 10_000m;
        if (randomValue < options.TimeoutRate)
        {
            throw new BlockchainTransferTimeoutException("Simulated blockchain gateway timeout.");
        }

        if (randomValue < options.TimeoutRate + options.RejectRate)
        {
            throw new BlockchainTransferRejectedException("Simulated blockchain gateway rejected the transfer.");
        }
    }
}

file static class SimulatedTransferRules
{
    public static void ValidateAddress(Regex pattern, string destinationAddress, string assetName)
    {
        if (string.IsNullOrWhiteSpace(destinationAddress) || !pattern.IsMatch(destinationAddress.Trim()))
        {
            throw new BlockchainTransferRejectedException($"{assetName} destination address is invalid.");
        }
    }
    public static void ValidateFee(decimal networkFee, decimal minFee, decimal maxFee, string assetName)
    {
        if (networkFee < minFee || networkFee > maxFee)
        {
            throw new BlockchainTransferRejectedException($"{assetName} network fee must be between {minFee} and {maxFee}.");
        }
    }
}
