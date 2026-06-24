using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Domain.Aggregates;

public sealed class BrokeredCryptoBuy
{
    public Guid Id { get; }
    public string CustomerAccountId { get; }
    public AssetSymbol AssetSymbol { get; }
    public QuoteCurrency QuoteCurrency { get; }
    public decimal RequestedQuantity { get; }
    public decimal InternalFillQuantity { get; }
    public decimal ExternalHedgeQuantity { get; }
    public decimal UnitPrice { get; }
    public decimal TotalCost { get; }
    public DateTimeOffset QuotedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public bool RequiresExternalHedge => ExternalHedgeQuantity > 0m;

    private BrokeredCryptoBuy(
        string customerAccountId,
        AssetSymbol assetSymbol,
        QuoteCurrency quoteCurrency,
        decimal requestedQuantity,
        decimal internalFillQuantity,
        decimal externalHedgeQuantity,
        decimal unitPrice,
        DateTimeOffset quotedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerAccountId);
        if (requestedQuantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedQuantity), requestedQuantity, "Requested quantity must be greater than zero.");
        }

        if (internalFillQuantity < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(internalFillQuantity), internalFillQuantity, "Internal fill quantity cannot be negative.");
        }

        if (externalHedgeQuantity < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(externalHedgeQuantity), externalHedgeQuantity, "External hedge quantity cannot be negative.");
        }

        if (checked(internalFillQuantity + externalHedgeQuantity) != requestedQuantity)
        {
            throw new ArgumentOutOfRangeException(nameof(externalHedgeQuantity), externalHedgeQuantity, "Internal fill and external hedge quantity must equal requested quantity.");
        }

        if (unitPrice <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), unitPrice, "Unit price must be greater than zero.");
        }

        if (expiresAtUtc <= quotedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), expiresAtUtc, "Quote expiration must be later than quote time.");
        }

        Id = Guid.CreateVersion7();
        CustomerAccountId = customerAccountId.Trim();
        AssetSymbol = assetSymbol;
        QuoteCurrency = quoteCurrency;
        RequestedQuantity = requestedQuantity;
        InternalFillQuantity = internalFillQuantity;
        ExternalHedgeQuantity = externalHedgeQuantity;
        UnitPrice = unitPrice;
        TotalCost = checked(requestedQuantity * unitPrice);
        QuotedAtUtc = quotedAtUtc.ToUniversalTime();
        ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
    }

    public static BrokeredCryptoBuy Create(
        string customerAccountId,
        AssetSymbol assetSymbol,
        QuoteCurrency quoteCurrency,
        decimal requestedQuantity,
        decimal availableInternalInventory,
        decimal marketUnitPrice,
        decimal spreadBasisPoints,
        DateTimeOffset quotedAtUtc,
        TimeSpan quoteTtl)
    {
        if (availableInternalInventory < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(availableInternalInventory), availableInternalInventory, "Available internal inventory cannot be negative.");
        }

        if (marketUnitPrice <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(marketUnitPrice), marketUnitPrice, "Market unit price must be greater than zero.");
        }

        if (spreadBasisPoints < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(spreadBasisPoints), spreadBasisPoints, "Spread basis points cannot be negative.");
        }

        if (quoteTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(quoteTtl), quoteTtl, "Quote TTL must be greater than zero.");
        }

        var internalFill = Math.Min(requestedQuantity, availableInternalInventory);
        var externalHedge = checked(requestedQuantity - internalFill);
        var spreadMultiplier = 1m + (spreadBasisPoints / 10_000m);
        var pricedUnit = checked(marketUnitPrice * spreadMultiplier);

        return new BrokeredCryptoBuy(
            customerAccountId,
            assetSymbol,
            quoteCurrency,
            requestedQuantity,
            internalFill,
            externalHedge,
            pricedUnit,
            quotedAtUtc,
            quotedAtUtc.Add(quoteTtl));
    }
}
