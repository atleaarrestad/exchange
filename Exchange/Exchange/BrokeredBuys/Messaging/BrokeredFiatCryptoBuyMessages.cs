namespace Exchange.BrokeredBuys.Messaging;

public sealed record SubmitBrokeredFiatCryptoBuy(
    Guid CorrelationId,
    Guid QuoteId,
    string ClientOrderId,
    string CustomerAccountId,
    string AssetSymbol,
    decimal Quantity,
    string QuoteCurrency,
    decimal? MaxUnitPrice,
    decimal? MaxTotalCost,
    DateTimeOffset SubmittedAtUtc);

public sealed record ReserveFiatForBrokeredBuy(
    Guid CorrelationId,
    Guid QuoteId,
    string ClientOrderId,
    string CustomerAccountId,
    string FiatCurrency,
    DateTimeOffset ReservedAtUtc);

public sealed record FiatReservedForBrokeredBuy(
    Guid CorrelationId,
    string ClientOrderId,
    string CustomerAccountId,
    string FiatCurrency,
    decimal ReservedAmount,
    DateTimeOffset ReservedAtUtc);

public sealed record FiatReservationFailedForBrokeredBuy(
    Guid CorrelationId,
    string ClientOrderId,
    string CustomerAccountId,
    string FailureReason,
    DateTimeOffset FailedAtUtc);

public sealed record BookCryptoForBrokeredBuy(
    Guid CorrelationId,
    Guid QuoteId,
    string ClientOrderId,
    string CustomerAccountId,
    string AssetSymbol,
    decimal Quantity,
    string QuoteCurrency,
    decimal? MaxUnitPrice,
    decimal? MaxTotalCost,
    DateTimeOffset RequestedAtUtc);

public sealed record CryptoBookedForBrokeredBuy(
    Guid CorrelationId,
    string ClientOrderId,
    string CustomerAccountId,
    string QuoteCurrency,
    decimal TotalCost,
    DateTimeOffset ExecutedAtUtc);

public sealed record CryptoBookingFailedForBrokeredBuy(
    Guid CorrelationId,
    string ClientOrderId,
    string CustomerAccountId,
    string FailureReason,
    DateTimeOffset FailedAtUtc);

public sealed record CaptureFiatForBrokeredBuy(
    Guid CorrelationId,
    string ClientOrderId,
    string CustomerAccountId,
    string FiatCurrency,
    decimal CapturedAmount,
    DateTimeOffset CapturedAtUtc);

public sealed record FiatCapturedForBrokeredBuy(
    Guid CorrelationId,
    string ClientOrderId,
    string CustomerAccountId,
    string FiatCurrency,
    decimal CapturedAmount,
    DateTimeOffset CapturedAtUtc);

public sealed record FiatCaptureFailedForBrokeredBuy(
    Guid CorrelationId,
    string ClientOrderId,
    string CustomerAccountId,
    string FailureReason,
    DateTimeOffset FailedAtUtc);

public sealed record ReleaseFiatReservationForBrokeredBuy(
    Guid CorrelationId,
    string ClientOrderId,
    string CustomerAccountId,
    string FiatCurrency,
    decimal ReleasedAmount,
    DateTimeOffset ReleasedAtUtc);

public sealed record FiatReservationReleasedForBrokeredBuy(
    Guid CorrelationId,
    string ClientOrderId,
    string CustomerAccountId,
    string FiatCurrency,
    decimal ReleasedAmount,
    DateTimeOffset ReleasedAtUtc);

public sealed record FiatReservationReleaseFailedForBrokeredBuy(
    Guid CorrelationId,
    string ClientOrderId,
    string CustomerAccountId,
    string FailureReason,
    DateTimeOffset FailedAtUtc);
