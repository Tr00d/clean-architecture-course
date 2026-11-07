using Bank.Domain.Models;
using Bank.Domain.Services;
using Bank.Domain.Tests;
using Xunit;

namespace Bank.Adapters.Cli.Tests;

public class AtmScreenTests
{
    private static readonly AccountId Id = new(1);
    private static readonly DateTime Now = new(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOperations _operations = new();

    [Fact]
    public async Task Deposit_IncreasesTheBalance()
    {
        await RunAsync("deposit 100");

        var account = await _operations.AccountOfAsync(Id);
        Assert.Equal(new Amount(100), account.Balance);
    }

    [Fact]
    public async Task Withdrawal_DecreasesTheBalance()
    {
        await RunAsync("deposit 100", "withdraw 30");

        var account = await _operations.AccountOfAsync(Id);
        Assert.Equal(new Amount(70), account.Balance);
    }

    [Fact]
    public async Task Withdrawal_DisplaysTheReason_WhenRefused()
    {
        var output = await RunAsync("withdraw 30");

        Assert.Contains("Insufficient funds", output);
    }

    [Fact]
    public async Task Statement_DisplaysTheOperationsAndTheBalance()
    {
        var output = await RunAsync("deposit 100", "statement");

        Assert.Contains("2021-01-10 | 100 | 100", output);
        Assert.Contains("Balance: 100", output);
    }

    private async Task<string> RunAsync(params string[] commands)
    {
        var input = new StringReader(string.Join(Environment.NewLine, commands));
        var output = new StringWriter();
        var service = new AccountService(_operations, new FixedClock(Now));
        var screen = new AtmScreen(Id, service, service, service, input, output);

        await screen.RunAsync();

        return output.ToString();
    }
}
