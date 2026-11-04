using LedgerService.Server.Services.Screening;

namespace LedgerService.Tests.Infrastructure;

/// <summary>A screening service we control: set the outcome, add delay or make it throw, and see what was asked.</summary>
public class StubScreeningClient(ScreeningOutcome outcome = ScreeningOutcome.Approved) : IScreeningClient
{
    private readonly List<(string RequestId, string From, string To, decimal Amount)> _calls = [];

    /// <summary>What screening answers. Can be changed during a test, e.g. to simulate it recovering.</summary>
    public ScreeningOutcome Outcome { get; set; } = outcome;

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>If set, screening throws it (an unexpected failure, not a "service down" answer).</summary>
    public Exception? Throws { get; set; }

    /// <summary>Every call so far, in order. A snapshot, safe to read while requests are still running.</summary>
    public IReadOnlyList<(string RequestId, string From, string To, decimal Amount)> Calls
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    public async Task<ScreeningOutcome> ScreenAsync(
        string requestId, string fromAccount, string toAccount, decimal amount, CancellationToken ct = default)
    {
        lock (_calls)
        {
            _calls.Add((requestId, fromAccount, toAccount, amount));
        }

        if (Delay > TimeSpan.Zero)
            await Task.Delay(Delay, ct);
        if (Throws is not null)
            throw Throws;
        return Outcome;
    }
}
