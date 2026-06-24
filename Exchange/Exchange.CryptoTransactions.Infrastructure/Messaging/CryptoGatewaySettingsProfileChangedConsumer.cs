using Exchange.CryptoTransactions.Application.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public sealed class CryptoGatewaySettingsProfileChangedConsumer(
    IKrakenGatewayOptionsProvider krakenGatewayOptionsProvider,
    ILogger<CryptoGatewaySettingsProfileChangedConsumer> logger) : IConsumer<CryptoGatewaySettingsProfileChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CryptoGatewaySettingsProfileChangedIntegrationEvent> context)
    {
        await krakenGatewayOptionsProvider.RefreshAsync(context.Message.ProfileId, context.CancellationToken);
        logger.LogInformation(
            "Applied runtime crypto gateway settings profile {ProfileId} ({ChangeType}).",
            context.Message.ProfileId,
            context.Message.ChangeType);
    }
}
