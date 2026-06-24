using MassTransit;

namespace Exchange.BrokeredBuys.Messaging;

public sealed class BrokeredFiatCryptoBuyStateMachine : MassTransitStateMachine<BrokeredFiatCryptoBuySagaState>
{
    public State ReservingFiat { get; private set; } = null!;
    public State BookingCrypto { get; private set; } = null!;
    public State CapturingFiat { get; private set; } = null!;
    public State ReversingCrypto { get; private set; } = null!;
    public State ReleasingReservation { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<SubmitBrokeredFiatCryptoBuy> Submitted { get; private set; } = null!;
    public Event<FiatReservedForBrokeredBuy> FiatReserved { get; private set; } = null!;
    public Event<FiatReservationFailedForBrokeredBuy> FiatReservationFailed { get; private set; } = null!;
    public Event<CryptoBookedForBrokeredBuy> CryptoBooked { get; private set; } = null!;
    public Event<CryptoBookingFailedForBrokeredBuy> CryptoBookingFailed { get; private set; } = null!;
    public Event<FiatCapturedForBrokeredBuy> FiatCaptured { get; private set; } = null!;
    public Event<FiatCaptureFailedForBrokeredBuy> FiatCaptureFailed { get; private set; } = null!;
    public Event<CryptoBookingReversedForBrokeredBuy> CryptoBookingReversed { get; private set; } = null!;
    public Event<CryptoBookingReverseFailedForBrokeredBuy> CryptoBookingReverseFailed { get; private set; } = null!;
    public Event<FiatReservationReleasedForBrokeredBuy> FiatReservationReleased { get; private set; } = null!;
    public Event<FiatReservationReleaseFailedForBrokeredBuy> FiatReservationReleaseFailed { get; private set; } = null!;

    public BrokeredFiatCryptoBuyStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => Submitted, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => FiatReserved, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => FiatReservationFailed, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => CryptoBooked, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => CryptoBookingFailed, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => FiatCaptured, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => FiatCaptureFailed, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => CryptoBookingReversed, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => CryptoBookingReverseFailed, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => FiatReservationReleased, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => FiatReservationReleaseFailed, x => x.CorrelateById(context => context.Message.CorrelationId));

        Initially(
            When(Submitted)
                .Then(context =>
                {
                    context.Saga.QuoteId = context.Message.QuoteId;
                    context.Saga.ClientOrderId = context.Message.ClientOrderId.Trim();
                    context.Saga.CustomerAccountId = context.Message.CustomerAccountId.Trim();
                    context.Saga.AssetSymbol = context.Message.AssetSymbol.Trim();
                    context.Saga.Quantity = context.Message.Quantity;
                    context.Saga.QuoteCurrency = context.Message.QuoteCurrency.Trim();
                    context.Saga.MaxUnitPrice = context.Message.MaxUnitPrice;
                    context.Saga.MaxTotalCost = context.Message.MaxTotalCost;
                    context.Saga.FailureReason = string.Empty;
                    context.Saga.ReservedAmount = 0m;
                    context.Saga.CapturedAmount = 0m;
                })
                .Publish(context => new ReserveFiatForBrokeredBuy(
                    context.Saga.CorrelationId,
                    context.Saga.QuoteId,
                    context.Saga.ClientOrderId,
                    context.Saga.CustomerAccountId,
                    context.Saga.QuoteCurrency,
                    DateTimeOffset.UtcNow))
                .TransitionTo(ReservingFiat));

        During(ReservingFiat,
            When(FiatReserved)
                .Then(context => { context.Saga.ReservedAmount = context.Message.ReservedAmount; })
                .Publish(context => new BookCryptoForBrokeredBuy(
                    context.Saga.CorrelationId,
                    context.Saga.QuoteId,
                    context.Saga.ClientOrderId,
                    context.Saga.CustomerAccountId,
                    context.Saga.AssetSymbol,
                    context.Saga.Quantity,
                    context.Saga.QuoteCurrency,
                    context.Saga.MaxUnitPrice,
                    context.Saga.MaxTotalCost,
                    DateTimeOffset.UtcNow))
                .TransitionTo(BookingCrypto),
            When(FiatReservationFailed)
                .Then(context => { context.Saga.FailureReason = context.Message.FailureReason; })
                .TransitionTo(Failed)
                .Finalize());

        During(BookingCrypto,
            When(CryptoBooked)
                .Then(context => { context.Saga.CapturedAmount = context.Message.TotalCost; })
                .Publish(context => new CaptureFiatForBrokeredBuy(
                    context.Saga.CorrelationId,
                    context.Saga.ClientOrderId,
                    context.Saga.CustomerAccountId,
                    context.Message.QuoteCurrency,
                    context.Message.TotalCost,
                    DateTimeOffset.UtcNow))
                .TransitionTo(CapturingFiat),
            When(CryptoBookingFailed)
                .Then(context => { context.Saga.FailureReason = context.Message.FailureReason; })
                .Publish(context => new ReleaseFiatReservationForBrokeredBuy(
                    context.Saga.CorrelationId,
                    context.Saga.ClientOrderId,
                    context.Saga.CustomerAccountId,
                    context.Saga.QuoteCurrency,
                    context.Saga.ReservedAmount,
                    DateTimeOffset.UtcNow))
                .TransitionTo(ReleasingReservation));

        During(CapturingFiat,
            When(FiatCaptured)
                .TransitionTo(Completed)
                .Finalize(),
            When(FiatCaptureFailed)
                .Then(context => { context.Saga.FailureReason = context.Message.FailureReason; })
                .Publish(context => new ReverseCryptoBookingForBrokeredBuy(
                    context.Saga.CorrelationId,
                    context.Saga.ClientOrderId,
                    context.Saga.CustomerAccountId,
                    context.Saga.AssetSymbol,
                    context.Message.FailureReason,
                    DateTimeOffset.UtcNow))
                .TransitionTo(ReversingCrypto));

        During(ReversingCrypto,
            When(CryptoBookingReversed)
                .Publish(context => new ReleaseFiatReservationForBrokeredBuy(
                    context.Saga.CorrelationId,
                    context.Saga.ClientOrderId,
                    context.Saga.CustomerAccountId,
                    context.Saga.QuoteCurrency,
                    context.Saga.ReservedAmount,
                    DateTimeOffset.UtcNow))
                .TransitionTo(ReleasingReservation),
            When(CryptoBookingReverseFailed)
                .Then(context => { context.Saga.FailureReason = context.Message.FailureReason; })
                .TransitionTo(Failed)
                .Finalize());

        During(ReleasingReservation,
            When(FiatReservationReleased)
                .TransitionTo(Failed)
                .Finalize(),
            When(FiatReservationReleaseFailed)
                .Then(context => { context.Saga.FailureReason = context.Message.FailureReason; })
                .TransitionTo(Failed)
                .Finalize());

        SetCompletedWhenFinalized();
    }
}
