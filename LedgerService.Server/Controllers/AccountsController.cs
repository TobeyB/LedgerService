using LedgerService.Server.Models.Dtos;
using LedgerService.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LedgerService.Server.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController(ILedgerService ledger, IAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountDto>>> GetAccounts(CancellationToken ct) =>
        Ok((await ledger.GetAccountsAsync(ct)).Select(AccountDto.From));

    [HttpPost("{accountNumber}/deposits")]
    public async Task<ActionResult<TransactionDto>> Deposit(
        string accountNumber, AmountRequest request, CancellationToken ct)
    {
        var result = await ledger.DepositAsync(accountNumber, request.Amount, ct);
        return result.IsSuccess ? Ok(TransactionDto.From(result.Value!)) : ToProblem(result.Error!.Value);
    }

    [HttpPost("{accountNumber}/withdrawals")]
    public async Task<ActionResult<TransactionDto>> Withdraw(
        string accountNumber, AmountRequest request, CancellationToken ct)
    {
        var result = await ledger.WithdrawAsync(accountNumber, request.Amount, ct);
        return result.IsSuccess ? Ok(TransactionDto.From(result.Value!)) : ToProblem(result.Error!.Value);
    }

    [HttpGet("{accountNumber}/audit")]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> GetAuditTrail(string accountNumber, CancellationToken ct) =>
        Ok((await audit.GetAuditTrailAsync(accountNumber, ct)).Select(AuditEntryDto.From));

    private ObjectResult ToProblem(LedgerError error) => error switch
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
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
}
