namespace Exchange.CryptoTransactions.Application.Validation;

public interface ICryptoGatewaySettingsCommandValidator
{
    void Validate(CreateCryptoGatewaySettingsProfileCommand command);

    void Validate(UpdateCryptoGatewaySettingsProfileCommand command);
}
