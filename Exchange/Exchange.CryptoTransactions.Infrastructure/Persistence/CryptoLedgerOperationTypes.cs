namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public static class CryptoLedgerOperationTypes
{
    public const string BrokeredCryptoBuy = "brokered_crypto_buy";
    public const string BrokeredCryptoBuyCompensation = "brokered_crypto_buy_compensation";
    public const string ExternalHedgeSettlement = "external_hedge_settlement";
}
