namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public static class InfrastructureConfigurationKeys
{
    public const string IdempotencySqliteConnectionString = "CryptoTransactions:Idempotency:SqliteConnectionString";
    public const string DefaultIdempotencySqliteConnectionString = "Data Source=exchange-crypto-idempotency.db";
}
