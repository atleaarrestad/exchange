namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public static class InfrastructureConfigurationKeys
{
    public const string IdempotencyConnectionString = "CryptoTransactions:Idempotency:ConnectionString";
    public const string DefaultIdempotencyConnectionString = "Host=localhost;Port=5432;Database=exchange;Username=exchange;Password=exchange_dev_password";
    public const string BrokeredTradingSection = "CryptoTransactions:BrokeredTrading";
    public const string TimeoutReconciliationScanIntervalSeconds = "CryptoTransactions:Idempotency:TimeoutReconciliation:ScanIntervalSeconds";
    public const int DefaultTimeoutReconciliationScanIntervalSeconds = 30;
    public const string TimeoutReconciliationStaleAfterSeconds = "CryptoTransactions:Idempotency:TimeoutReconciliation:StaleAfterSeconds";
    public const int DefaultTimeoutReconciliationStaleAfterSeconds = 45;
    public const string SettingsChangeOutboxSection = "CryptoTransactions:SettingsChangeOutbox";
    public const string SettingsChangeOutboxArchivalJobSection = "CryptoTransactions:CronJobs:SettingsChangeOutboxArchival";
    public const string KrakenGatewaySection = "CryptoTransactions:Gateways:Kraken";
    public const string BlockchainGatewayResilienceSection = "CryptoTransactions:Resilience:BlockchainGateway";
}
