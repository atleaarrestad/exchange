using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class EfCoreBrokeredCryptoBuyQuoteStore(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory) : IBrokeredCryptoBuyQuoteStore
{
    public async Task StoreAsync(BrokeredCryptoBuyQuote quote, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quote);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new BrokeredCryptoBuyQuoteEntity
        {
            Id = quote.QuoteId,
            CustomerAccountId = quote.CustomerAccountId,
            AssetSymbol = quote.AssetSymbol,
            QuoteCurrency = quote.QuoteCurrency,
            Quantity = quote.Quantity,
            InternalFillQuantity = quote.InternalFillQuantity,
            ExternalHedgeQuantity = quote.ExternalHedgeQuantity,
            UnitPrice = quote.UnitPrice,
            TotalCost = quote.TotalCost,
            MarketPriceObservedAtUtc = quote.MarketPriceObservedAtUtc,
            QuotedAtUtc = quote.QuotedAtUtc,
            ExpiresAtUtc = quote.ExpiresAtUtc,
            RequiresExternalHedge = quote.RequiresExternalHedge,
            PriceSource = quote.PriceSource,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.BrokeredCryptoBuyQuotes.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<BrokeredCryptoBuyQuote?> GetByIdAsync(Guid quoteId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var quote = await dbContext.BrokeredCryptoBuyQuotes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == quoteId, cancellationToken);

        if (quote is null)
        {
            return null;
        }

        return new BrokeredCryptoBuyQuote(
            quote.Id,
            quote.CustomerAccountId,
            quote.AssetSymbol,
            quote.QuoteCurrency,
            quote.Quantity,
            quote.InternalFillQuantity,
            quote.ExternalHedgeQuantity,
            quote.UnitPrice,
            quote.TotalCost,
            quote.MarketPriceObservedAtUtc,
            quote.QuotedAtUtc,
            quote.ExpiresAtUtc,
            quote.RequiresExternalHedge,
            quote.PriceSource);
    }
}
