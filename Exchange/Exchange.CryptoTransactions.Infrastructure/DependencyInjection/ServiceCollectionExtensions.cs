using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCryptoTransactionsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetValue<string>(InfrastructureConfigurationKeys.IdempotencySqliteConnectionString)
            ?? InfrastructureConfigurationKeys.DefaultIdempotencySqliteConnectionString;

        services.AddDbContextFactory<CryptoTransactionsDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<ISubmitCryptoTransferCommandValidator, SubmitCryptoTransferCommandValidator>();
        services.AddSingleton<ICryptoTransferIdempotencyStore>(serviceProvider =>
            new SqliteCryptoTransferIdempotencyStore(serviceProvider.GetRequiredService<IDbContextFactory<CryptoTransactionsDbContext>>()));
        services.AddSingleton<ICryptoTransferService, CryptoTransferService>();
        services.AddSingleton<ICryptoTransferFundsReservationGateway, UnconfiguredCryptoTransferFundsReservationGateway>();
        services.AddSingleton<IBlockchainTransferGateway, UnconfiguredBlockchainTransferGateway>();
        return services;
    }
}
