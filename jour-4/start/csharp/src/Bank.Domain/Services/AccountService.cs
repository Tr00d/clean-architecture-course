using Bank.Domain.Models;
using Bank.Domain.Ports;

namespace Bank.Domain.Services;

public class AccountService(IOperations operations, IClock clock)
    : IForDepositing, IForWithdrawing, IForPrintingStatements
{
    public async Task DepositAsync(AccountId accountId, Amount amount)
    {
        var account = await operations.AccountOfAsync(accountId);
        account.Deposit(amount, clock.Now());
        await operations.SaveAsync(account);
    }

    public async Task WithdrawAsync(AccountId accountId, Amount amount)
    {
        var account = await operations.AccountOfAsync(accountId);
        account.Withdraw(amount, clock.Now());
        await operations.SaveAsync(account);
    }

    public async Task<IReadOnlyList<StatementLine>> PrintStatementAsync(AccountId accountId)
    {
        var account = await operations.AccountOfAsync(accountId);
        return account.Statement();
    }
}
