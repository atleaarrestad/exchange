namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public static class InfrastructureConfigurationKeys
{
    public const string IdempotencySqliteConnectionString = "CryptoTransactions:Idempotency:SqliteConnectionString";
    public const string DefaultIdempotencySqliteConnectionString = "Data Source=exchange-crypto-idempotency.db";
    public const string TimeoutReconciliationScanIntervalSeconds = "CryptoTransactions:Idempotency:TimeoutReconciliation:ScanIntervalSeconds";
    public const int DefaultTimeoutReconciliationScanIntervalSeconds = 30;
    public const string TimeoutReconciliationStaleAfterSeconds = "CryptoTransactions:Idempotency:TimeoutReconciliation:StaleAfterSeconds";
    public const int DefaultTimeoutReconciliationStaleAfterSeconds = 45;
    public const string KrakenGatewaySection = "CryptoTransactions:Gateways:Kraken";
}
