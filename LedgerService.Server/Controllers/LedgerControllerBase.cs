using LedgerService.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LedgerService.Server.Controllers;

public abstract class LedgerControllerBase : ControllerBase
{
    protected ObjectResult ToProblem(LedgerError error)
    {
        switch (error)
        {
            case LedgerError.ScreeningUnavailable:
            case LedgerError.RequestInProgress:
                Response.Headers.RetryAfter = "1";
                break;
        }

        return error switch
        {
            LedgerError.InvalidAmount => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid amount",
                detail: "Amount must be greater than zero and have at most two decimals."),
            LedgerError.AccountNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Account not found"),
            LedgerError.InsufficientFunds => Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Insufficient funds"),
            LedgerError.SameAccount => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid transfer",
                detail: "The source and target accounts must be different."),
            LedgerError.ScreeningRejected => Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Transfer refused by screening",
                detail: "The transfer was not approved. No money was moved."),
            LedgerError.ScreeningUnavailable => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Screening unavailable",
                detail: "The transfer could not be screened, so it was not made. No money was moved. Try again later."),
            LedgerError.IdempotencyKeyReused => Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Idempotency-Key reused",
                detail: "This Idempotency-Key was already used for a different request."),
            LedgerError.RequestInProgress => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Request in progress",
                detail: "A request with this Idempotency-Key is still being processed. Retry shortly."),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
