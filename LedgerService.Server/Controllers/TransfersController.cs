using LedgerService.Server.Models.Dtos;
using LedgerService.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LedgerService.Server.Controllers;

[ApiController]
[Route("api/transfers")]
public class TransfersController(ILedgerService ledger) : LedgerControllerBase
{
    private const int MaxKeyLength = 200;

    /// <summary>
    /// Send an Idempotency-Key header to make retries safe: the transfer happens at most once
    /// and every retry gets the answer of the first request.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Transfer(
        TransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (idempotencyKey is not null && (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > MaxKeyLength))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Idempotency-Key",
                detail: $"The Idempotency-Key must be 1 to {MaxKeyLength} characters.");

        var result = await ledger.TransferAsync(request.FromAccount, request.ToAccount, request.Amount, idempotencyKey, ct);
        return result.IsSuccess ? Ok(TransactionDto.From(result.Value!)) : ToProblem(result.Error!.Value);
    }
}
