using MassTransit;

namespace Exchange.BrokeredBuys.Messaging;

public sealed class BrokeredFiatCryptoBuySagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;

    public Guid QuoteId { get; set; }
    public string ClientOrderId { get; set; } = string.Empty;
    public string CustomerAccountId { get; set; } = string.Empty;
    public string AssetSymbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string QuoteCurrency { get; set; } = string.Empty;
    public decimal? MaxUnitPrice { get; set; }
    public decimal? MaxTotalCost { get; set; }

    public decimal ReservedAmount { get; set; }
    public decimal CapturedAmount { get; set; }
    public string FailureReason { get; set; } = string.Empty;
}
