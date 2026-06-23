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

        var simulationSection = configuration.GetSection(SimulationConfigurationKeys.CryptoTransactionsSimulationSection);
        var blockchainGatewayOptions = SimulatedBlockchainTransferGatewayOptions.FromConfiguration(
            simulationSection);
        var fundsReservationOptions = SimulatedFundsReservationOptions.FromConfiguration(
            simulationSection);

        services.AddSingleton(blockchainGatewayOptions);
        services.AddSingleton(fundsReservationOptions);
        services.AddSingleton<IBlockchainTransferStrategy, SimulatedBitcoinTransferStrategy>();
        services.AddSingleton<IBlockchainTransferStrategy, SimulatedEtherTransferStrategy>();
        services.AddSingleton<ICryptoTransferFundsReservationGateway, SimulatedCryptoTransferFundsReservationGateway>();
        services.AddSingleton<IBlockchainTransferGateway, SimulatedBlockchainTransferGateway>();
        return services;
    }
}
