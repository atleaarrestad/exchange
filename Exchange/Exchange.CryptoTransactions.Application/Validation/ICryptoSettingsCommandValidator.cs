namespace Exchange.CryptoTransactions.Application.Validation;

public interface ICryptoSettingsCommandValidator
{
    void Validate(CreateCryptoSettingsProfileCommand command);

    void Validate(UpdateCryptoSettingsProfileCommand command);
}
