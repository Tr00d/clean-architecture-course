using Bank.Api.Models;
using Bank.Business;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Controllers;

[ApiController]
[Route("accounts/{accountId:int}")]
public class AccountsController(AccountService accountService) : ControllerBase
{
    [HttpGet("operations")]
    public async Task<IReadOnlyList<StatementLineResponse>> GetStatement(int accountId)
    {
        var lines = await accountService.GetStatementAsync(accountId);

        return lines
            .Select(line => new StatementLineResponse(line.Date, line.Amount, line.Balance))
            .ToList();
    }

    [HttpPost("deposits")]
    public async Task<IActionResult> Deposit(int accountId, AmountRequest request)
    {
        await accountService.DepositAsync(accountId, request.Amount);
        return Created();
    }

    [HttpPost("withdrawals")]
    public async Task<IActionResult> Withdraw(int accountId, AmountRequest request)
    {
        try
        {
            await accountService.WithdrawAsync(accountId, request.Amount);
            return Created();
        }
        catch (InsufficientFundsException exception)
        {
            return UnprocessableEntity(exception.Message);
        }
    }
}
