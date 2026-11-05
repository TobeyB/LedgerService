using LedgerService.Server.Models.Dtos;
using LedgerService.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LedgerService.Server.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController(ILedgerService ledger, IAuditService audit) : LedgerControllerBase
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

    [HttpGet("{accountNumber}/transactions")]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> GetTransactions(string accountNumber, CancellationToken ct)
    {
        var result = await ledger.GetTransactionsAsync(accountNumber, ct);
        return result.IsSuccess ? Ok(result.Value!.Select(TransactionDto.From)) : ToProblem(result.Error!.Value);
    }

    [HttpGet("{accountNumber}/audit")]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> GetAuditTrail(string accountNumber, CancellationToken ct) =>
        Ok((await audit.GetAuditTrailAsync(accountNumber, ct)).Select(AuditEntryDto.From));
}
