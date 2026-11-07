using Bank.Domain.Models;
using Xunit;

namespace Bank.Domain.Tests;

public class AccountTests
{
    private static readonly AccountId Id = new(1);
    private static readonly DateTime Date = new(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc);

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
}
