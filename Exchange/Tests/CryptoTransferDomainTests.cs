using Exchange.CryptoTransactions.Domain.Aggregates;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Tests;

[TestClass]
public sealed class CryptoTransferDomainTests
{
    [TestMethod]
    public void AssetSymbol_Parse_NormalizesCasing()
    {
        var symbol = AssetSymbol.Parse("eth");
        Assert.AreEqual(AssetSymbol.Ether, symbol);
    }

    [TestMethod]
    public void AssetSymbol_Parse_WithUnsupportedSymbol_Throws()
    {
        var action = () => { AssetSymbol.Parse("usdt"); };
        Assert.ThrowsExactly<ArgumentException>(action);
    }

    [TestMethod]
    public void CryptoAmount_WithMoreThanEighteenDecimals_Throws()
    {
        var action = () => new CryptoAmount(AssetSymbol.Bitcoin, 0.1234567890123456789m);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(action);
    }

    [TestMethod]
    public void CryptoTransfer_TotalDebit_EqualsAmountPlusFee()
    {
        var transfer = new CryptoTransfer(
            "tx-001",
            "account-123",
            "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kygt080",
            new CryptoAmount(AssetSymbol.Ether, 1.125m),
            new NetworkFee(0.0035m),
            DateTimeOffset.UtcNow);

        Assert.AreEqual(1.1285m, transfer.TotalDebit);
        Assert.AreEqual(AssetSymbol.Ether, transfer.Amount.AssetSymbol);
    }
}
