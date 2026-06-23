using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using System.Text.RegularExpressions;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

public sealed class SimulatedBitcoinTransferStrategy(
    SimulatedBlockchainTransferGatewayOptions options) : IBlockchainTransferStrategy
{
    private readonly IAddressValidator addressValidator = new BitcoinAddressValidator();
    private readonly IFeeEstimator feeEstimator = new BitcoinFeeEstimator();
    private readonly ITransactionBuilder transactionBuilder = new BitcoinTransactionBuilder();
    private readonly ISigner signer = new SimulatedSigner(CryptoAssetSymbols.Bitcoin);
    private readonly IBroadcaster broadcaster = new SimulatedBroadcaster("btc");
    private readonly IConfirmationPolicy confirmationPolicy = new FixedConfirmationPolicy(3);

    public string AssetSymbol => CryptoAssetSymbols.Bitcoin;

    public async Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        addressValidator.Validate(request.DestinationAddress);
        var normalizedFee = feeEstimator.NormalizeFee(request);
        var builtTransaction = transactionBuilder.Build(request, normalizedFee);
        var signedTransaction = signer.Sign(builtTransaction);

        await SimulatedGatewayBehavior.SimulateGatewayDelayAndFailureAsync(options, cancellationToken);

        var result = await broadcaster.BroadcastAsync(signedTransaction, cancellationToken);
        return result with { RequiredConfirmations = confirmationPolicy.RequiredConfirmations };
    }
}

public sealed class SimulatedEtherTransferStrategy(
    SimulatedBlockchainTransferGatewayOptions options) : IBlockchainTransferStrategy
{
    private readonly IAddressValidator addressValidator = new EtherAddressValidator();
    private readonly IFeeEstimator feeEstimator = new EtherFeeEstimator();
    private readonly ITransactionBuilder transactionBuilder = new EtherTransactionBuilder();
    private readonly ISigner signer = new SimulatedSigner(CryptoAssetSymbols.Ether);
    private readonly IBroadcaster broadcaster = new SimulatedBroadcaster("eth");
    private readonly IConfirmationPolicy confirmationPolicy = new FixedConfirmationPolicy(12);

    public string AssetSymbol => CryptoAssetSymbols.Ether;

    public async Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        addressValidator.Validate(request.DestinationAddress);
        var normalizedFee = feeEstimator.NormalizeFee(request);
        var builtTransaction = transactionBuilder.Build(request, normalizedFee);
        var signedTransaction = signer.Sign(builtTransaction);

        await SimulatedGatewayBehavior.SimulateGatewayDelayAndFailureAsync(options, cancellationToken);

        var result = await broadcaster.BroadcastAsync(signedTransaction, cancellationToken);
        return result with { RequiredConfirmations = confirmationPolicy.RequiredConfirmations };
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

file sealed class BitcoinAddressValidator : IAddressValidator
{
    private static readonly Regex BtcAddressPattern = new("^(bc1|[13])[A-Za-z0-9]{20,90}$", RegexOptions.Compiled);

    public void Validate(string destinationAddress)
    {
        if (string.IsNullOrWhiteSpace(destinationAddress) || !BtcAddressPattern.IsMatch(destinationAddress.Trim()))
        {
            throw new BlockchainTransferRejectedException("Bitcoin destination address is invalid.");
        }
    }
}

file sealed class EtherAddressValidator : IAddressValidator
{
    private static readonly Regex EthAddressPattern = new("^0x[0-9a-fA-F]{40}$", RegexOptions.Compiled);

    public void Validate(string destinationAddress)
    {
        if (string.IsNullOrWhiteSpace(destinationAddress) || !EthAddressPattern.IsMatch(destinationAddress.Trim()))
        {
            throw new BlockchainTransferRejectedException("Ether destination address is invalid.");
        }
    }
}

file sealed class BitcoinFeeEstimator : IFeeEstimator
{
    private const decimal MinFee = 0.000001m;
    private const decimal MaxFee = 0.1m;

    public decimal NormalizeFee(BlockchainTransferRequest request)
    {
        if (request.NetworkFee < MinFee || request.NetworkFee > MaxFee)
        {
            throw new BlockchainTransferRejectedException($"Bitcoin network fee must be between {MinFee} and {MaxFee}.");
        }

        return request.NetworkFee;
    }
}

file sealed class EtherFeeEstimator : IFeeEstimator
{
    private const decimal MinFee = 0.000000001m;
    private const decimal MaxFee = 1m;

    public decimal NormalizeFee(BlockchainTransferRequest request)
    {
        if (request.NetworkFee < MinFee || request.NetworkFee > MaxFee)
        {
            throw new BlockchainTransferRejectedException($"Ether network fee must be between {MinFee} and {MaxFee}.");
        }

        return request.NetworkFee;
    }
}

file sealed class BitcoinTransactionBuilder : ITransactionBuilder
{
    public BuiltTransaction Build(BlockchainTransferRequest request, decimal networkFee)
    {
        var payload = $"type=utxo;inputs=simulated;outputs=2;amount={request.Amount};fee={networkFee}";
        return new BuiltTransaction(CryptoAssetSymbols.Bitcoin, payload, networkFee);
    }
}

file sealed class EtherTransactionBuilder : ITransactionBuilder
{
    public BuiltTransaction Build(BlockchainTransferRequest request, decimal networkFee)
    {
        var payload = $"type=account;nonce=simulated;gas=21000;value={request.Amount};fee={networkFee}";
        return new BuiltTransaction(CryptoAssetSymbols.Ether, payload, networkFee);
    }
}

file sealed class SimulatedSigner(string assetSymbol) : ISigner
{
    public SignedTransaction Sign(BuiltTransaction transaction)
    {
        var signedPayload = $"{assetSymbol.ToLowerInvariant()}-signed:{transaction.Payload}";
        return new SignedTransaction(transaction.AssetSymbol, signedPayload, transaction.NetworkFee);
    }
}

file sealed class SimulatedBroadcaster(string txPrefix) : IBroadcaster
{
    public Task<BlockchainTransferResult> BroadcastAsync(SignedTransaction transaction, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transactionId = $"{txPrefix}-{Guid.CreateVersion7():N}";
        return Task.FromResult(new BlockchainTransferResult(transactionId, DateTimeOffset.UtcNow));
    }
}

file sealed class FixedConfirmationPolicy(int requiredConfirmations) : IConfirmationPolicy
{
    public int RequiredConfirmations { get; } = requiredConfirmations;
}
