using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

namespace Tests;

[TestClass]
public sealed class SimulatedCryptoTransferFundsReservationGatewayTests
{
    [TestMethod]
    public async Task CommitAsync_WithoutActiveReservation_ThrowsInvalidOperationException()
    {
        var gateway = new SimulatedCryptoTransferFundsReservationGateway(new SimulatedFundsReservationOptions());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            gateway.CommitAsync("account-1", AssetSymbol.Bitcoin, "idem-1"));
    }

    [TestMethod]
    public async Task ReleaseAsync_WithoutActiveReservation_ThrowsInvalidOperationException()
    {
        var gateway = new SimulatedCryptoTransferFundsReservationGateway(new SimulatedFundsReservationOptions());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            gateway.ReleaseAsync("account-1", AssetSymbol.Ether, "idem-2"));
    }
}
