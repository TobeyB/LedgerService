using System.Net.Http.Json;
using System.Text.Json;
using LedgerService.Server.Models.Domain;

namespace LedgerService.Server.Services.Screening;

/// <summary>
/// Retries, timeouts and the circuit breaker are not handled here: they come from the resilience handler
/// added to this client's HttpClient in Program. This class only turns the final outcome into a <see cref="ScreeningOutcome"/>.
/// </summary>
public class ScreeningClient(HttpClient http, ILogger<ScreeningClient> logger) : IScreeningClient
{
    private record Request(string RequestId, string FromAccount, string ToAccount, decimal Amount, string Currency);

    private record Response(string? Decision);

    public async Task<ScreeningOutcome> ScreenAsync(
        string requestId, string fromAccount, string toAccount, decimal amount, CancellationToken ct = default)
    {
        var body = new Request(requestId, fromAccount, toAccount, amount, Money.Currency);

        try
        {
            using var response = await http.PostAsJsonAsync("v1/screenings", body, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Screening {RequestId} answered HTTP {Status}; treating as unavailable",
                    requestId, (int)response.StatusCode);
                return ScreeningOutcome.Unavailable;
            }

            var decision = (await response.Content.ReadFromJsonAsync<Response>(ct))?.Decision;
            return decision switch
            {
                "Approve" => ScreeningOutcome.Approved,
                "Reject" => ScreeningOutcome.Rejected,
                _ => Unrecognised(requestId)
            };
        }
        catch (JsonException)
        {
            return Unrecognised(requestId);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Timeout, connection failure or open circuit: no answer, so no transfer.
            logger.LogWarning(ex, "Screening {RequestId} failed; treating as unavailable", requestId);
            return ScreeningOutcome.Unavailable;
        }
    }

    private ScreeningOutcome Unrecognised(string requestId)
    {
        logger.LogWarning("Screening {RequestId} answered with something unrecognised; treating as unavailable", requestId);
        return ScreeningOutcome.Unavailable;
    }
}
