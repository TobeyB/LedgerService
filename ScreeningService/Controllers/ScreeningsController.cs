using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ScreeningService.Models;
using ScreeningService.Options;
using ScreeningService.Services;

namespace ScreeningService.Controllers;

// Fake third-party screening service. Not production code.
//  - Takes 50-1500 ms, and sometimes answers 429 or 503 with a Retry-After header.
//  - The same requestId always gets the same decision.
//  - Any screening involving the rejected account is rejected; everything else is approved.
[ApiController]
[Route("v1/screenings")]
public class ScreeningsController(IOptions<ScreeningOptions> options, DecisionCache cache) : ControllerBase
{
    private readonly ScreeningOptions _options = options.Value;

    [HttpPost]
    public async Task<ActionResult<ScreeningResponse>> Screen(ScreeningRequest request)
    {
        await Task.Delay(Random.Shared.Next(_options.MinDelayMs, _options.MaxDelayMs + 1));

        if (Random.Shared.NextDouble() < _options.FailureRate)
        {
            Response.Headers.RetryAfter = "1";
            return StatusCode(Random.Shared.Next(2) == 0
                ? StatusCodes.Status429TooManyRequests
                : StatusCodes.Status503ServiceUnavailable);
        }

        var decision = string.IsNullOrEmpty(request.RequestId)
            ? Decide(request)
            : cache.GetOrAdd(request.RequestId, () => Decide(request));

        return new ScreeningResponse(request.RequestId, decision);
    }

    private Decision Decide(ScreeningRequest request) =>
        request.FromAccount == _options.RejectedAccount || request.ToAccount == _options.RejectedAccount
            ? Decision.Reject
            : Decision.Approve;
}
