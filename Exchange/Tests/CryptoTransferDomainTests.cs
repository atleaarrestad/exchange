using Exchange.CryptoTransactions.Domain.Aggregates;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Tests;

[TestClass]
public sealed class CryptoTransferDomainTests
{
    [TestMethod]
    public void CryptoAmount_WithMoreThanEighteenDecimals_Throws()
    {
        var action = () => new CryptoAmount("BTC", 0.1234567890123456789m);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(action);
    }

    [TestMethod]
    public void CryptoTransfer_TotalDebit_EqualsAmountPlusFee()
    {
        var transfer = new CryptoTransfer(
            "tx-001",
            "account-123",
            "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kygt080",
            new CryptoAmount("ETH", 1.125m),
            new NetworkFee(0.0035m),
            DateTimeOffset.UtcNow);

        Assert.AreEqual(1.1285m, transfer.TotalDebit);
        Assert.AreEqual("ETH", transfer.Amount.AssetSymbol);
    }
}
