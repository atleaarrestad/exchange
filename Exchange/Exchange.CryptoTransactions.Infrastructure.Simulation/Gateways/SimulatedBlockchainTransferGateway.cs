using Exchange.CryptoTransactions.Application.Contracts;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

public sealed class SimulatedBlockchainTransferGateway(
    SimulatedBlockchainTransferGatewayOptions options) : IBlockchainTransferGateway
{
    public async Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var delayMs = Random.Shared.Next(options.MinLatencyMs, options.MaxLatencyMs + 1);
        await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);

        var randomValue = NextRandomDecimal();
        if (randomValue < options.TimeoutRate)
        {
            throw new BlockchainTransferTimeoutException("Simulated blockchain gateway timeout.");
        }

        if (randomValue < options.TimeoutRate + options.RejectRate)
        {
            throw new BlockchainTransferRejectedException("Simulated blockchain gateway rejected the transfer.");
        }

        var transactionId = $"sim-{Guid.CreateVersion7():N}";
        return new BlockchainTransferResult(transactionId, DateTimeOffset.UtcNow);
    }

    private static decimal NextRandomDecimal()
    {
        return Random.Shared.Next(0, 10_000) / 10_000m;
    }
}
