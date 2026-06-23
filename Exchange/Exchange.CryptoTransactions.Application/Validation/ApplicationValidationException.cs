namespace Exchange.CryptoTransactions.Application.Validation;

public sealed class ApplicationValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ApplicationValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("The request failed validation.")
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }
}
