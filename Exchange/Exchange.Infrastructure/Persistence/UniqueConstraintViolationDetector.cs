namespace Exchange.Infrastructure.Persistence;

public static class UniqueConstraintViolationDetector
{
    public static bool IsUniqueConstraintViolation(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var sqlState = exception.GetType().GetProperty("SqlState")?.GetValue(exception) as string;
        if (string.Equals(sqlState, "23505", StringComparison.Ordinal))
        {
            return true;
        }

        var innerException = exception.InnerException;
        if (innerException is not null && IsUniqueConstraintViolation(innerException))
        {
            return true;
        }

        var message = exception.Message;
        return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate key value violates unique constraint", StringComparison.OrdinalIgnoreCase);
    }
}
