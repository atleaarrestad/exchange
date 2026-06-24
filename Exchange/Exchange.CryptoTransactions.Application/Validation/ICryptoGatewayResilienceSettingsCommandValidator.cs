namespace Exchange.CryptoTransactions.Application.Validation;

public interface ICryptoGatewayResilienceSettingsCommandValidator
{
    void Validate(CreateCryptoGatewayResilienceSettingsProfileCommand command);

    void Validate(UpdateCryptoGatewayResilienceSettingsProfileCommand command);
}
