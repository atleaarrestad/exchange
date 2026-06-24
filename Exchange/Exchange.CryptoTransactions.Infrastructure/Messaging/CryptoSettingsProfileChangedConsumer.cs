using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Messaging;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public sealed class CryptoSettingsProfileChangedConsumer(
    IBrokeredTradingPolicyProvider tradingPolicyProvider,
    ILogger<CryptoSettingsProfileChangedConsumer> logger) : IConsumer<CryptoSettingsProfileChangedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CryptoSettingsProfileChangedIntegrationEvent> context)
    {
        await tradingPolicyProvider.RefreshAsync(context.Message.ProfileId, context.CancellationToken);
        logger.LogInformation(
            "Applied runtime crypto settings profile {ProfileId} ({ChangeType}).",
            context.Message.ProfileId,
            context.Message.ChangeType);
    }
}
