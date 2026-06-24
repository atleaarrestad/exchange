using Exchange.BrokeredBuys.Messaging;
using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Exchange.Contracts;
using Exchange.FiatTransactions.Domain.ValueObjects;
using Exchange.FiatTransactions.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Exchange.Simulation;

public interface IBrokeredBuySimulationRunner
{
    Task<StartBrokeredBuySimulationResponse> StartAsync(
        StartBrokeredBuySimulationRequest request,
        CancellationToken cancellationToken);

    Task<ResetBrokeredBuySimulationDataResponse> ResetDataAsync(CancellationToken cancellationToken);
}

public sealed class BrokeredBuySimulationRunner(
    IDbContextFactory<CryptoTransactionsDbContext> cryptoDbContextFactory,
    IDbContextFactory<FiatTransactionsDbContext> fiatDbContextFactory,
    IBrokeredCryptoBuyService brokeredCryptoBuyService,
    IPublishEndpoint publishEndpoint) : IBrokeredBuySimulationRunner
{
    public async Task<StartBrokeredBuySimulationResponse> StartAsync(
        StartBrokeredBuySimulationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            throw new ApplicationValidationException(validationErrors);
        }

        var normalizedCustomerAccountId = request.CustomerAccountId.Trim();
        var assetSymbol = AssetSymbol.Parse(request.AssetSymbol, nameof(request.AssetSymbol));
        var quoteCurrency = QuoteCurrency.Parse(request.QuoteCurrency, nameof(request.QuoteCurrency));
        var fiatCurrency = FiatCurrency.Parse(request.QuoteCurrency, nameof(request.QuoteCurrency));
        var normalizedClientOrderId = string.IsNullOrWhiteSpace(request.ClientOrderId)
            ? $"sim-order-{Guid.CreateVersion7()}"
            : request.ClientOrderId.Trim();
        var startedAtUtc = DateTimeOffset.UtcNow;

        await ResetDataAsync(cancellationToken);
        await SeedSimulationStateAsync(
            normalizedCustomerAccountId,
            fiatCurrency,
            request.CustomerFiatAvailableBalance,
            request.PlatformBitcoinInventory,
            request.PlatformEtherInventory,
            startedAtUtc,
            cancellationToken);

        var quote = await brokeredCryptoBuyService.QuoteAsync(
            new QuoteBrokeredCryptoBuyCommand(
                normalizedCustomerAccountId,
                assetSymbol.Value,
                request.Quantity,
                quoteCurrency.Value),
            cancellationToken);

        var correlationId = Guid.CreateVersion7();
        await publishEndpoint.Publish(
            new SubmitBrokeredFiatCryptoBuy(
                correlationId,
                quote.QuoteId,
                normalizedClientOrderId,
                normalizedCustomerAccountId,
                assetSymbol.Value,
                request.Quantity,
                quoteCurrency.Value,
                request.MaxUnitPrice,
                request.MaxTotalCost,
                startedAtUtc),
            cancellationToken);

        return new StartBrokeredBuySimulationResponse(
            correlationId,
            quote.QuoteId,
            normalizedClientOrderId,
            normalizedCustomerAccountId,
            assetSymbol.Value,
            request.Quantity,
            quoteCurrency.Value,
            request.CustomerFiatAvailableBalance,
            request.PlatformBitcoinInventory,
            request.PlatformEtherInventory,
            startedAtUtc,
            "submitted");
    }

    public async Task<ResetBrokeredBuySimulationDataResponse> ResetDataAsync(CancellationToken cancellationToken)
    {
        await ResetBuySimulationStateAsync(cancellationToken);
        return new ResetBrokeredBuySimulationDataResponse(DateTimeOffset.UtcNow, "reset");
    }

    private async Task ResetBuySimulationStateAsync(CancellationToken cancellationToken)
    {
        await using var cryptoContext = await cryptoDbContextFactory.CreateDbContextAsync(cancellationToken);
        await cryptoContext.BrokeredFiatCryptoBuySagaStates.ExecuteDeleteAsync(cancellationToken);
        await cryptoContext.BrokeredCryptoBuyQuotes.ExecuteDeleteAsync(cancellationToken);
        await cryptoContext.BrokeredCryptoBuyExecutions.ExecuteDeleteAsync(cancellationToken);
        await cryptoContext.ExternalHedgeBatchEntries.ExecuteDeleteAsync(cancellationToken);
        await cryptoContext.ExternalHedgeExecutionRecords.ExecuteDeleteAsync(cancellationToken);
        await cryptoContext.CryptoLedgerEntries.ExecuteDeleteAsync(cancellationToken);
        await cryptoContext.CryptoLedgerTransactions.ExecuteDeleteAsync(cancellationToken);
        await cryptoContext.CryptoOwnershipPositions.ExecuteDeleteAsync(cancellationToken);
        await cryptoContext.PlatformInventoryPositions.ExecuteDeleteAsync(cancellationToken);

        await using var fiatContext = await fiatDbContextFactory.CreateDbContextAsync(cancellationToken);
        await fiatContext.BrokeredCryptoBuySettlements.ExecuteDeleteAsync(cancellationToken);
        await fiatContext.FiatLedgerEntries.ExecuteDeleteAsync(cancellationToken);
        await fiatContext.FiatLedgerTransactions.ExecuteDeleteAsync(cancellationToken);
        await fiatContext.FiatBalancePositions.ExecuteDeleteAsync(cancellationToken);
    }

    private async Task SeedSimulationStateAsync(
        string customerAccountId,
        FiatCurrency fiatCurrency,
        decimal customerFiatAvailableBalance,
        decimal platformBitcoinInventory,
        decimal platformEtherInventory,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var cryptoContext = await cryptoDbContextFactory.CreateDbContextAsync(cancellationToken);
        cryptoContext.PlatformInventoryPositions.AddRange(
            new PlatformInventoryPositionEntity
            {
                AssetSymbol = AssetSymbol.Bitcoin.Value,
                AvailableQuantity = platformBitcoinInventory,
                UpdatedAtUtc = now
            },
            new PlatformInventoryPositionEntity
            {
                AssetSymbol = AssetSymbol.Ether.Value,
                AvailableQuantity = platformEtherInventory,
                UpdatedAtUtc = now
            });
        await cryptoContext.SaveChangesAsync(cancellationToken);

        await using var fiatContext = await fiatDbContextFactory.CreateDbContextAsync(cancellationToken);
        fiatContext.FiatBalancePositions.Add(new FiatBalancePositionEntity
        {
            FiatCurrency = fiatCurrency.Value,
            AccountKind = FiatLedgerAccountKinds.CustomerAvailable,
            AccountId = customerAccountId,
            AvailableAmount = customerFiatAvailableBalance,
            UpdatedAtUtc = now
        });
        await fiatContext.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, string[]> Validate(StartBrokeredBuySimulationRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.CustomerAccountId))
        {
            errors[nameof(request.CustomerAccountId)] = ["CustomerAccountId is required."];
        }

        if (!AssetSymbol.TryParse(request.AssetSymbol, out _))
        {
            errors[nameof(request.AssetSymbol)] = ["AssetSymbol must be one of: BTC, ETH."];
        }

        if (!QuoteCurrency.TryParse(request.QuoteCurrency, out _))
        {
            errors[nameof(request.QuoteCurrency)] = ["QuoteCurrency must be one of: NOK."];
        }

        if (request.Quantity <= 0m)
        {
            errors[nameof(request.Quantity)] = ["Quantity must be greater than zero."];
        }

        if (request.CustomerFiatAvailableBalance <= 0m)
        {
            errors[nameof(request.CustomerFiatAvailableBalance)] = ["CustomerFiatAvailableBalance must be greater than zero."];
        }

        if (request.PlatformBitcoinInventory < 0m)
        {
            errors[nameof(request.PlatformBitcoinInventory)] = ["PlatformBitcoinInventory cannot be negative."];
        }

        if (request.PlatformEtherInventory < 0m)
        {
            errors[nameof(request.PlatformEtherInventory)] = ["PlatformEtherInventory cannot be negative."];
        }

        return errors;
    }
}
