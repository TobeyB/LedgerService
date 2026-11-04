namespace LedgerService.Server.Services;

/// <summary>Values are explicit because some are stored in idempotency records. Never renumber.</summary>
public enum LedgerError
{
    InvalidAmount = 1,
    AccountNotFound = 2,
    InsufficientFunds = 3,
    SameAccount = 4,
    ScreeningRejected = 5,

    /// <summary>The screening service gave no usable answer, so no money was moved. Safe to retry later.</summary>
    ScreeningUnavailable = 6,

    /// <summary>The Idempotency-Key was already used for a different request.</summary>
    IdempotencyKeyReused = 7,

    /// <summary>The first request with this Idempotency-Key is still being processed.</summary>
    RequestInProgress = 8
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
