using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

namespace Tests;

[TestClass]
public sealed class SimulatedCryptoTransferFundsReservationGatewayTests
{
    [TestMethod]
    public async Task CommitAsync_WithoutActiveReservation_IsIdempotent()
    {
        var gateway = new SimulatedCryptoTransferFundsReservationGateway(new SimulatedFundsReservationOptions());

        await gateway.CommitAsync("account-1", AssetSymbol.Bitcoin, "idem-1");
    }

    [TestMethod]
    public async Task ReleaseAsync_WithoutActiveReservation_IsIdempotent()
    {
        var gateway = new SimulatedCryptoTransferFundsReservationGateway(new SimulatedFundsReservationOptions());

        await gateway.ReleaseAsync("account-1", AssetSymbol.Ether, "idem-2");
    }
}
