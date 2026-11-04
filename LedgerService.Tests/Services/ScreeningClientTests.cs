using System.Net;
using System.Text;
using System.Text.Json;
using LedgerService.Server.Services.Screening;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LedgerService.Tests.Services;

public class ScreeningClientTests
{
    /// <summary>Plays back scripted answers (the last one repeats) and records what was sent.</summary>
    private class ScriptedHandler(params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] script)
        : HttpMessageHandler
    {
        private readonly List<string> _bodies = [];

        public IReadOnlyList<string> Bodies { get { lock (_bodies) return _bodies.ToList(); } }
        public int Calls => Bodies.Count;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            int index;
            var body = await request.Content!.ReadAsStringAsync(ct);
            lock (_bodies)
            {
                _bodies.Add(body);
                index = _bodies.Count - 1;
            }
            return await script[Math.Min(index, script.Length - 1)](request, ct);
        }
    }

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Answer(
        HttpStatusCode status, string? json = null) => (_, _) =>
    {
        var response = new HttpResponseMessage(status);
        if (json is not null) response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return Task.FromResult(response);
    };

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Decision(string decision) =>
        Answer(HttpStatusCode.OK, $$"""{"requestId":"r1","decision":"{{decision}}"}""");

    private static readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Hangs =
        async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(); };

    private static Task<ScreeningOutcome> Screen(IScreeningClient client, CancellationToken ct = default) =>
        client.ScreenAsync("r1", "NB-1001", "NB-1002", 15000.50m, ct);

    /// <summary>The client on its own: no retries, just turning answers into outcomes.</summary>
    private static ScreeningClient Plain(ScriptedHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://screening.test") }, NullLogger<ScreeningClient>.Instance);

    /// <summary>The client with the same kind of standard resilience handler Program adds, with short timings.</summary>
    private static ServiceProvider Resilient(ScriptedHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient<IScreeningClient, ScreeningClient>(http => http.BaseAddress = new Uri("http://screening.test"))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddStandardResilienceHandler(o =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(200);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(3);
                o.Retry.MaxRetryAttempts = 2;
                o.Retry.Delay = TimeSpan.FromMilliseconds(10);
                o.Retry.UseJitter = false;
                o.Retry.ShouldRetryAfterHeader = true;
            });
        return services.BuildServiceProvider();
    }

    // --- Turning answers into outcomes ---

    [Fact]
    public async Task Approve_is_returned_as_Approved_and_the_request_carries_everything_needed()
    {
        var handler = new ScriptedHandler(Decision("Approve"));

        var outcome = await Screen(Plain(handler));

        Assert.Equal(ScreeningOutcome.Approved, outcome);
        using var sent = JsonDocument.Parse(Assert.Single(handler.Bodies));
        Assert.Equal("r1", sent.RootElement.GetProperty("requestId").GetString());
        Assert.Equal("NB-1001", sent.RootElement.GetProperty("fromAccount").GetString());
        Assert.Equal("NB-1002", sent.RootElement.GetProperty("toAccount").GetString());
        Assert.Equal(15000.50m, sent.RootElement.GetProperty("amount").GetDecimal());
        Assert.Equal("SEK", sent.RootElement.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task Reject_is_returned_as_Rejected()
    {
        Assert.Equal(ScreeningOutcome.Rejected, await Screen(Plain(new ScriptedHandler(Decision("Reject")))));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Error_statuses_are_Unavailable(HttpStatusCode status)
    {
        Assert.Equal(ScreeningOutcome.Unavailable, await Screen(Plain(new ScriptedHandler(Answer(status)))));
    }

    [Fact]
    public async Task Connection_failures_are_Unavailable()
    {
        var handler = new ScriptedHandler((_, _) => throw new HttpRequestException("connection refused"));

        Assert.Equal(ScreeningOutcome.Unavailable, await Screen(Plain(handler)));
    }

    [Theory]
    [InlineData("""{"decision":"Maybe"}""")]
    [InlineData("""{"decision":null}""")]
    [InlineData("""{}""")]
    [InlineData("not json at all")]
    public async Task An_answer_that_is_not_understood_is_Unavailable_never_Approved(string body)
    {
        var handler = new ScriptedHandler(Answer(HttpStatusCode.OK, body));

        Assert.Equal(ScreeningOutcome.Unavailable, await Screen(Plain(handler)));
    }

    [Fact]
    public async Task Cancellation_by_the_caller_is_not_swallowed()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Screen(Plain(new ScriptedHandler(Hangs)), cts.Token));
    }

    // --- With the standard resilience handler ---

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Rate_limited_or_unavailable_is_retried_with_the_same_request_id_and_then_succeeds(HttpStatusCode first)
    {
        var handler = new ScriptedHandler(Answer(first), Decision("Approve"));
        await using var provider = Resilient(handler);

        var outcome = await Screen(provider.GetRequiredService<IScreeningClient>());

        Assert.Equal(ScreeningOutcome.Approved, outcome);
        Assert.Equal(2, handler.Calls);
        Assert.All(handler.Bodies, b => Assert.Contains("\"requestId\":\"r1\"", b));
    }

    [Fact]
    public async Task A_service_that_keeps_failing_ends_Unavailable_after_a_bounded_number_of_attempts()
    {
        var handler = new ScriptedHandler(Answer(HttpStatusCode.ServiceUnavailable));
        await using var provider = Resilient(handler);

        var outcome = await Screen(provider.GetRequiredService<IScreeningClient>());

        Assert.Equal(ScreeningOutcome.Unavailable, outcome);
        Assert.Equal(3, handler.Calls); // the first try and two retries
    }

    [Fact]
    public async Task A_service_that_never_answers_times_out_and_ends_Unavailable()
    {
        var handler = new ScriptedHandler(Hangs);
        await using var provider = Resilient(handler);

        var started = DateTime.UtcNow;
        var outcome = await Screen(provider.GetRequiredService<IScreeningClient>());

        Assert.Equal(ScreeningOutcome.Unavailable, outcome);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(3.5), "must stop within the total time budget");
    }

    [Fact]
    public async Task A_timeout_followed_by_an_answer_is_retried()
    {
        var handler = new ScriptedHandler(Hangs, Decision("Reject"));
        await using var provider = Resilient(handler);

        Assert.Equal(ScreeningOutcome.Rejected, await Screen(provider.GetRequiredService<IScreeningClient>()));
        Assert.Equal(2, handler.Calls);
    }
}
