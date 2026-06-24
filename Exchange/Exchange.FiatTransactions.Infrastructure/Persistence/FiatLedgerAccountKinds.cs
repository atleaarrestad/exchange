namespace Exchange.FiatTransactions.Infrastructure.Persistence;

public static class FiatLedgerAccountKinds
{
    public const string CustomerAvailable = "customer_available";
    public const string CustomerReserved = "customer_reserved";
    public const string PlatformTradeClearing = "platform_trade_clearing";
    public const string PlatformBankCash = "platform_bank_cash";
}
