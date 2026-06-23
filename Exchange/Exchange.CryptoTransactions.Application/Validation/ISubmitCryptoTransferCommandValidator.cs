namespace Exchange.CryptoTransactions.Application.Validation;

public interface ISubmitCryptoTransferCommandValidator
{
    void Validate(SubmitCryptoTransferCommand command);
}
