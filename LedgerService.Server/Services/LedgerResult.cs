namespace LedgerService.Server.Services;

public enum LedgerError
{
    InvalidAmount,
    AccountNotFound
}

/// <summary>Outcome of a ledger operation: either a value or a business error. Not for unexpected failures.</summary>
public class LedgerResult<T>
{
    private LedgerResult(T? value, LedgerError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public LedgerError? Error { get; }
    public bool IsSuccess => Error is null;

    public static LedgerResult<T> Success(T value) => new(value, null);
    public static LedgerResult<T> Fail(LedgerError error) => new(default, error);
}
