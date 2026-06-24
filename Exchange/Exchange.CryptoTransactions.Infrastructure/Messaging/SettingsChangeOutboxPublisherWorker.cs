using Exchange.CryptoTransactions.Application.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public sealed class SettingsChangeOutboxPublisherWorker(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    IBus bus,
    ILogger<SettingsChangeOutboxPublisherWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingEntriesAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed while publishing pending settings outbox entries.");
            }
        }
    }

    private async Task PublishPendingEntriesAsync(CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pendingEntries = await context.SettingsChangeOutboxEntries
            .Where(entry => entry.PublishedAtUtc == null)
            .OrderBy(entry => entry.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (pendingEntries.Count == 0)
        {
            return;
        }

        foreach (var entry in pendingEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await PublishEntryAsync(entry, cancellationToken);
                entry.PublishedAtUtc = DateTimeOffset.UtcNow;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                entry.PublishAttemptCount = checked(entry.PublishAttemptCount + 1);
                logger.LogError(
                    exception,
                    "Failed publishing settings outbox entry {OutboxEntryId} of type {MessageType}.",
                    entry.Id,
                    entry.MessageType);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task PublishEntryAsync(SettingsChangeOutboxEntryEntity entry, CancellationToken cancellationToken)
    {
        switch (entry.MessageType)
        {
            case SettingsChangeOutboxMessageTypes.CryptoSettingsProfileChanged:
                await bus.Publish(
                    Deserialize<CryptoSettingsProfileChangedIntegrationEvent>(entry.PayloadJson),
                    cancellationToken);
                return;
            case SettingsChangeOutboxMessageTypes.CryptoGatewaySettingsProfileChanged:
                await bus.Publish(
                    Deserialize<CryptoGatewaySettingsProfileChangedIntegrationEvent>(entry.PayloadJson),
                    cancellationToken);
                return;
            default:
                throw new InvalidOperationException($"Unknown outbox message type '{entry.MessageType}'.");
        }
    }

    private static T Deserialize<T>(string payloadJson)
    {
        var value = JsonSerializer.Deserialize<T>(payloadJson);
        return value ?? throw new InvalidOperationException($"Unable to deserialize payload to {typeof(T).Name}.");
    }
}
