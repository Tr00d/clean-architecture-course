using Bank.Domain.Models;
using Xunit;

namespace Bank.Domain.Tests;

public class AccountTests
{
    private static readonly AccountId Id = new(1);
    private static readonly DateTime Date = new(2021, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Deposit_IncreasesTheBalance()
    {
        var account = new Account(Id, []);

        account.Deposit(new Amount(100), Date);

        Assert.Equal(new Amount(100), account.Balance);
    }

    [Fact]
    public void Withdrawal_DecreasesTheBalance()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(100), Date);

        account.Withdraw(new Amount(30), Date);

        Assert.Equal(new Amount(70), account.Balance);
    }

    [Fact]
    public void Withdrawal_IsRefused_WhenAboveTheBalance()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(100), Date);

        Assert.Throws<InsufficientFundsException>(() => account.Withdraw(new Amount(100.01m), Date));

        Assert.Equal(new Amount(100), account.Balance);
    }

    [Fact]
    public void Withdrawal_IsAccepted_WhenEqualToTheBalance()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(100), Date);

        account.Withdraw(new Amount(100), Date);

        Assert.Equal(Amount.Zero, account.Balance);
    }

    [Fact]
    public void Statement_ListsOperationsNewestFirstWithRunningBalance()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(1000), new DateTime(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        account.Deposit(new Amount(2000), new DateTime(2021, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        account.Withdraw(new Amount(500), new DateTime(2021, 1, 20, 0, 0, 0, DateTimeKind.Utc));

        var statement = account.Statement();

        StatementLine[] expected =
        [
            new(new DateTime(2021, 1, 20, 0, 0, 0, DateTimeKind.Utc), new Amount(-500), new Amount(2500)),
            new(new DateTime(2021, 1, 15, 0, 0, 0, DateTimeKind.Utc), new Amount(2000), new Amount(3000)),
            new(new DateTime(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc), new Amount(1000), new Amount(1000))
        ];
        Assert.Equal(expected, statement);
    }

    [Fact]
    public void Statement_SortsOperationsByDate_WhateverTheirOrder()
    {
        var account = new Account(
            Id,
            [
                new Operation(new Amount(-50), new DateTime(2021, 2, 2, 0, 0, 0, DateTimeKind.Utc)),
                new Operation(new Amount(200), new DateTime(2021, 2, 1, 0, 0, 0, DateTimeKind.Utc))
            ]);

        var statement = account.Statement();

        Amount[] expected = [new Amount(150), new Amount(200)];
        Assert.Equal(expected, statement.Select(line => line.Balance));
    }

    [Fact]
    public void NewOperations_ContainsOnlyWhatWasAddedToTheHistory()
    {
        var account = new Account(Id, [new Operation(new Amount(100), Date)]);

        account.Deposit(new Amount(50), Date);

        var operation = Assert.Single(account.NewOperations);
        Assert.Equal(new Operation(new Amount(50), Date), operation);
    }

    [Fact]
    public void Withdrawal_IsRefused_WhenAboveTheLimit()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(5000), Date);

        Assert.Throws<WithdrawalLimitExceededException>(() => account.Withdraw(new Amount(2500.01m), Date));

        Assert.Equal(new Amount(5000), account.Balance);
    }

    [Fact]
    public void Withdrawal_IsRefused_WhenWithdrawalsAddUpAboveTheLimit()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(5000), Date);
        account.Withdraw(new Amount(1500), Date);

        Assert.Throws<WithdrawalLimitExceededException>(() => account.Withdraw(new Amount(1500), Date.AddHours(1)));
    }

    [Fact]
    public void Withdrawal_IsAccepted_WhenPreviousWithdrawalsAre24HoursOld()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(5000), Date);
        account.Withdraw(new Amount(1500), Date);

        account.Withdraw(new Amount(1500), Date.AddHours(24));

        Assert.Equal(new Amount(2000), account.Balance);
    }

    [Fact]
    public void Withdrawal_IsAccepted_WhenEqualToTheLimit()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(5000), Date);

        account.Withdraw(new Amount(2500), Date);

        Assert.Equal(new Amount(2500), account.Balance);
    }

    [Fact]
    public void Withdrawal_IsRefused_WhenAboveTheNightLimit_AtNightfall()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(1000), Date);
        var nightfall = new DateTime(2021, 1, 10, 22, 0, 0, DateTimeKind.Utc);

        Assert.Throws<NightWithdrawalLimitExceededException>(() => account.Withdraw(new Amount(500.01m), nightfall));

        Assert.Equal(new Amount(1000), account.Balance);
    }

    [Fact]
    public void Withdrawal_IsRefused_WhenAboveTheNightLimit_BeforeDawn()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(1000), Date);
        var beforeDawn = new DateTime(2021, 1, 10, 5, 59, 0, DateTimeKind.Utc);

        Assert.Throws<NightWithdrawalLimitExceededException>(() => account.Withdraw(new Amount(500.01m), beforeDawn));
    }

    [Fact]
    public void Withdrawal_IsAccepted_AboveTheNightLimit_AtDawn()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(1000), Date);
        var dawn = new DateTime(2021, 1, 10, 6, 0, 0, DateTimeKind.Utc);

        account.Withdraw(new Amount(600), dawn);

        Assert.Equal(new Amount(400), account.Balance);
    }
}
