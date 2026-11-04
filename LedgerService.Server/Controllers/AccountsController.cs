using LedgerService.Server.Models.Dtos;
using LedgerService.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LedgerService.Server.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController(ILedgerService ledger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountDto>>> GetAccounts(CancellationToken ct) =>
        Ok(await ledger.GetAccountsAsync(ct));
}
