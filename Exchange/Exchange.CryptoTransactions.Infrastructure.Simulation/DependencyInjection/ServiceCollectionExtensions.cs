using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCryptoTransactionsSimulation(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = SimulatedBlockchainTransferGatewayOptions.FromConfiguration(
            configuration.GetSection(SimulationConfigurationKeys.CryptoTransactionsSimulationSection));

        services.AddSingleton(options);
        services.AddSingleton<IBlockchainTransferGateway, SimulatedBlockchainTransferGateway>();
        return services;
    }
}
