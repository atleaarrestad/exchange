using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public sealed class CryptoTransferSubmissionWorker(
    ICryptoTransferIdempotencyStore idempotencyStore,
    CryptoTransferSubmissionProcessor processor,
    ILogger<CryptoTransferSubmissionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FallbackPendingAge = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Crypto transfer submission worker iteration failed.");
            }
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var pending = await idempotencyStore.GetPendingOlderThanAsync(DateTimeOffset.UtcNow - FallbackPendingAge, cancellationToken);
        foreach (var operation in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await processor.ProcessOperationAsync(operation, cancellationToken);
        }
    }
}
