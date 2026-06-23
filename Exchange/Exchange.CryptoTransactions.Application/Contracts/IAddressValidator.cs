namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IAddressValidator
{
    void Validate(string destinationAddress);
}
