namespace LedgerService.Server.Services.Screening;

public enum ScreeningOutcome
{
    Approved,
    Rejected,

    /// <summary>
    /// No usable answer (down, slow, rate limited, or an answer we do not understand).
    /// Callers must treat this as "do not move money".
    /// </summary>
    Unavailable
}

public interface IScreeningClient
{
    /// <summary>
    /// Asks the external screening service about a transfer. Never throws for service problems:
    /// they are returned as <see cref="ScreeningOutcome.Unavailable"/>. Throws only if <paramref name="ct"/> is cancelled.
    /// The same <paramref name="requestId"/> must be used for every retry of the same transfer, so the
    /// service gives the same decision and does not bill it again.
    /// </summary>
    Task<ScreeningOutcome> ScreenAsync(
        string requestId, string fromAccount, string toAccount, decimal amount, CancellationToken ct = default);
}
