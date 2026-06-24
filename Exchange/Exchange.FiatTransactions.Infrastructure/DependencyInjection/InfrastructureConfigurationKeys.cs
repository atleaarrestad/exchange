namespace Exchange.FiatTransactions.Infrastructure.DependencyInjection;

public static class InfrastructureConfigurationKeys
{
    public const string ConnectionString = "FiatTransactions:Database:ConnectionString";
    public const string DefaultConnectionString = "Host=localhost;Port=5432;Database=exchange;Username=exchange;Password=exchange_dev_password";
    public const string RunMigrationsOnStartup = "FiatTransactions:Database:RunMigrationsOnStartup";
}
