namespace Exchange.FiatTransactions.Infrastructure.Persistence;

public static class FiatLedgerOperationTypes
{
    public const string BrokeredCryptoBuyReservation = "brokered_crypto_buy_reservation";
    public const string BrokeredCryptoBuyReservationRelease = "brokered_crypto_buy_reservation_release";
    public const string BrokeredCryptoBuySettlement = "brokered_crypto_buy_settlement";
    public const string BankSettlement = "bank_settlement";
}
