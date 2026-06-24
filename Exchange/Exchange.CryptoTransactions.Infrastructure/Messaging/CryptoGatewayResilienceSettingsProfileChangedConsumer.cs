using Exchange.CryptoTransactions.Application.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public sealed class CryptoGatewayResilienceSettingsProfileChangedConsumer(
    IBlockchainGatewayResiliencePolicyOptionsProvider policyOptionsProvider,
    ILogger<CryptoGatewayResilienceSettingsProfileChangedConsumer> logger) : IConsumer<CryptoGatewayResilienceSettingsProfileChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CryptoGatewayResilienceSettingsProfileChangedIntegrationEvent> context)
    {
        await policyOptionsProvider.RefreshAsync(context.Message.ProfileId, context.CancellationToken);
        logger.LogInformation(
            "Applied runtime crypto gateway resilience settings profile {ProfileId} ({ChangeType}).",
            context.Message.ProfileId,
            context.Message.ChangeType);
    }
}
