using Bank.Domain.Models;
using Bank.Domain.Ports;
using Bank.Domain.Services;
using Xunit;

namespace Bank.Domain.Tests;

public class AccountServiceTests
{
    private static readonly AccountId Id = new(1);
    private static readonly DateTime Now = new(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOperations _operations = new();
    private readonly IForDepositing _depositing;
    private readonly IForWithdrawing _withdrawing;
    private readonly IForPrintingStatements _printingStatements;

    public AccountServiceTests()
    {
        var service = new AccountService(_operations, new FixedClock(Now));
        _depositing = service;
        _withdrawing = service;
        _printingStatements = service;
    }

    [Fact]
    public async Task Deposit_SavesTheOperationAtTheCurrentDate()
    {
        await _depositing.DepositAsync(Id, new Amount(100));

        var account = await _operations.AccountOfAsync(Id);
        var operation = Assert.Single(account.History);
        Assert.Equal(new Operation(new Amount(100), Now), operation);
    }

    [Fact]
    public async Task Withdraw_SavesTheOperationAtTheCurrentDate()
    {
        await _depositing.DepositAsync(Id, new Amount(100));

        await _withdrawing.WithdrawAsync(Id, new Amount(30));

        var account = await _operations.AccountOfAsync(Id);
        Assert.Equal(new Operation(new Amount(-30), Now), account.History[^1]);
    }

    [Fact]
    public async Task PrintStatement_ReturnsTheStatementOfTheAccount()
    {
        await _depositing.DepositAsync(Id, new Amount(100));

        var statement = await _printingStatements.PrintStatementAsync(Id);

        var line = Assert.Single(statement);
        Assert.Equal(new StatementLine(Now, new Amount(100), new Amount(100)), line);
    }
}
