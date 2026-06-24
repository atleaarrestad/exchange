using Exchange.FiatTransactions.Application.Contracts;
using Exchange.FiatTransactions.Infrastructure.Gateways;
using Exchange.FiatTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Exchange.FiatTransactions.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFiatTransactionsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetValue<string>(InfrastructureConfigurationKeys.ConnectionString)
            ?? InfrastructureConfigurationKeys.DefaultConnectionString;

        services.AddDbContextFactory<FiatTransactionsDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IFiatLedger, EfCoreFiatLedger>();

        return services;
    }
}
