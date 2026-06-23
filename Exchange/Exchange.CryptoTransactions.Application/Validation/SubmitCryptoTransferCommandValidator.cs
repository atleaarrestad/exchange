using System.Text.RegularExpressions;

namespace Exchange.CryptoTransactions.Application.Validation;

public sealed partial class SubmitCryptoTransferCommandValidator : ISubmitCryptoTransferCommandValidator
{
    private const string IdempotencyKey = nameof(SubmitCryptoTransferCommand.IdempotencyKey);
    private const string SourceAccountId = nameof(SubmitCryptoTransferCommand.SourceAccountId);
    private const string DestinationAddress = nameof(SubmitCryptoTransferCommand.DestinationAddress);
    private const string AssetSymbol = nameof(SubmitCryptoTransferCommand.AssetSymbol);
    private const string Amount = nameof(SubmitCryptoTransferCommand.Amount);
    private const string NetworkFee = nameof(SubmitCryptoTransferCommand.NetworkFee);

    public void Validate(SubmitCryptoTransferCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            AddError(errors, IdempotencyKey, "Idempotency key is required.");
        }
        else if (command.IdempotencyKey.Trim().Length > SubmitCryptoTransferValidationRules.IdempotencyKeyMaxLength)
        {
            AddError(errors, IdempotencyKey, $"Idempotency key cannot exceed {SubmitCryptoTransferValidationRules.IdempotencyKeyMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(command.SourceAccountId))
        {
            AddError(errors, SourceAccountId, "Source account id is required.");
        }
        else
        {
            var sourceAccountId = command.SourceAccountId.Trim();
            if (sourceAccountId.Length < SubmitCryptoTransferValidationRules.SourceAccountIdMinLength ||
                sourceAccountId.Length > SubmitCryptoTransferValidationRules.SourceAccountIdMaxLength)
            {
                AddError(errors, SourceAccountId, $"Source account id must be between {SubmitCryptoTransferValidationRules.SourceAccountIdMinLength} and {SubmitCryptoTransferValidationRules.SourceAccountIdMaxLength} characters.");
            }

            if (!AccountIdPattern().IsMatch(sourceAccountId))
            {
                AddError(errors, SourceAccountId, "Source account id may contain only letters, numbers, underscores, and dashes.");
            }
        }

        if (string.IsNullOrWhiteSpace(command.DestinationAddress))
        {
            AddError(errors, DestinationAddress, "Destination address is required.");
        }
        else
        {
            var destinationAddress = command.DestinationAddress.Trim();
            if (destinationAddress.Length < SubmitCryptoTransferValidationRules.DestinationAddressMinLength ||
                destinationAddress.Length > SubmitCryptoTransferValidationRules.DestinationAddressMaxLength)
            {
                AddError(errors, DestinationAddress, $"Destination address must be between {SubmitCryptoTransferValidationRules.DestinationAddressMinLength} and {SubmitCryptoTransferValidationRules.DestinationAddressMaxLength} characters.");
            }
        }

        if (string.IsNullOrWhiteSpace(command.AssetSymbol))
        {
            AddError(errors, AssetSymbol, "Asset symbol is required.");
        }
        else if (!global::Exchange.CryptoTransactions.Domain.ValueObjects.AssetSymbol.TryParse(command.AssetSymbol, out _))
        {
            AddError(errors, AssetSymbol, $"Asset symbol '{command.AssetSymbol.Trim()}' is not supported.");
        }

        if (command.Amount <= 0m)
        {
            AddError(errors, Amount, "Amount must be greater than zero.");
        }
        else if (command.Amount > SubmitCryptoTransferValidationRules.MaxAmount)
        {
            AddError(errors, Amount, $"Amount cannot exceed {SubmitCryptoTransferValidationRules.MaxAmount}.");
        }

        if (GetScale(command.Amount) > SubmitCryptoTransferValidationRules.MaxScale)
        {
            AddError(errors, Amount, $"Amount scale cannot exceed {SubmitCryptoTransferValidationRules.MaxScale} decimal places.");
        }

        if (command.NetworkFee < 0m)
        {
            AddError(errors, NetworkFee, "Network fee cannot be negative.");
        }
        else if (command.NetworkFee > SubmitCryptoTransferValidationRules.MaxNetworkFee)
        {
            AddError(errors, NetworkFee, $"Network fee cannot exceed {SubmitCryptoTransferValidationRules.MaxNetworkFee}.");
        }

        if (GetScale(command.NetworkFee) > SubmitCryptoTransferValidationRules.MaxScale)
        {
            AddError(errors, NetworkFee, $"Network fee scale cannot exceed {SubmitCryptoTransferValidationRules.MaxScale} decimal places.");
        }

        if (command.Amount > 0m && command.NetworkFee > command.Amount)
        {
            AddError(errors, NetworkFee, "Network fee cannot exceed transfer amount.");
        }

        if (errors.Count == 0)
        {
            return;
        }

        throw new ApplicationValidationException(errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.Ordinal));
    }

    private static void AddError(IDictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = new List<string>();
            errors[key] = messages;
        }

        messages.Add(message);
    }

    private static int GetScale(decimal value)
    {
        return (decimal.GetBits(value)[3] >> 16) & 0x7F;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex AccountIdPattern();
}
