using Bank.Adapters.Rest.Models;
using Bank.Domain.Models;
using Bank.Domain.Ports;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Adapters.Rest.Controllers;

[ApiController]
[Route("accounts/{accountId:int}")]
public class AccountsController(
    IForDepositing depositing,
    IForWithdrawing withdrawing,
    IForPrintingStatements printingStatements) : ControllerBase
{
    [HttpGet("operations")]
    public async Task<IReadOnlyList<StatementLineResponse>> GetStatement(int accountId)
    {
        var lines = await printingStatements.PrintStatementAsync(new AccountId(accountId));

        return lines
            .Select(line => new StatementLineResponse(line.Date, line.Amount.Value, line.Balance.Value))
            .ToList();
    }

    [HttpPost("deposits")]
    public async Task<IActionResult> Deposit(int accountId, AmountRequest request)
    {
        await depositing.DepositAsync(new AccountId(accountId), new Amount(request.Amount));
        return Created();
    }

    [HttpPost("withdrawals")]
    public async Task<IActionResult> Withdraw(int accountId, AmountRequest request)
    {
        try
        {
            await withdrawing.WithdrawAsync(new AccountId(accountId), new Amount(request.Amount));
            return Created();
        }
        catch (WithdrawalRefusedException exception)
        {
            return UnprocessableEntity(exception.Message);
        }
    }
}
