using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using Microsoft.Extensions.DependencyInjection;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCryptoTransactionsInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISubmitCryptoTransferCommandValidator, SubmitCryptoTransferCommandValidator>();
        services.AddSingleton<ICryptoTransferIdempotencyStore, InMemoryCryptoTransferIdempotencyStore>();
        services.AddSingleton<ICryptoTransferService, CryptoTransferService>();
        services.AddSingleton<IBlockchainTransferGateway, UnconfiguredBlockchainTransferGateway>();
        return services;
    }
}
