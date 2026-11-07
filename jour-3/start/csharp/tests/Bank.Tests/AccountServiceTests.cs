using Bank.Business;
using Bank.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supabase;

namespace Bank.Tests;

public class AccountServiceTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const int AccountId = 42;

    private readonly IConfiguration _configuration = factory.Services.GetRequiredService<IConfiguration>();

    public async ValueTask InitializeAsync() => await ClearOperationsAsync();

    public async ValueTask DisposeAsync() => await ClearOperationsAsync();

    [Fact]
    public async Task Statement_IsEmpty_WhenTheAccountHasNoOperations()
    {
        var service = new AccountService(_configuration);

        var statement = await service.GetStatementAsync(AccountId);

        Assert.Empty(statement);
    }

    [Fact]
    public async Task Statement_ListsOperationsNewestFirstWithRunningBalance()
    {
        var service = new AccountService(_configuration);
        await service.DepositAsync(AccountId, 1000);
        await service.DepositAsync(AccountId, 2000);
        await service.WithdrawAsync(AccountId, 500);

        var statement = await service.GetStatementAsync(AccountId);

        Assert.Equal([-500m, 2000m, 1000m], statement.Select(line => line.Amount));
        Assert.Equal([2500m, 3000m, 1000m], statement.Select(line => line.Balance));
    }

    [Fact]
    public async Task Statement_DatesOperationsWithTheCurrentTime()
    {
        var service = new AccountService(_configuration);
        var before = DateTime.UtcNow;

        await service.DepositAsync(AccountId, 1000);

        var line = Assert.Single(await service.GetStatementAsync(AccountId));
        Assert.InRange(line.Date, before.AddSeconds(-1), DateTime.UtcNow);
    }

    [Fact]
    public async Task Withdrawal_IsRefused_WhenAboveTheBalance()
    {
        var service = new AccountService(_configuration);
        await service.DepositAsync(AccountId, 100);

        await Assert.ThrowsAsync<InsufficientFundsException>(() => service.WithdrawAsync(AccountId, 100.01m));

        var line = Assert.Single(await service.GetStatementAsync(AccountId));
        Assert.Equal(100, line.Balance);
    }

    [Fact]
    public async Task Withdrawal_IsAccepted_WhenEqualToTheBalance()
    {
        var service = new AccountService(_configuration);
        await service.DepositAsync(AccountId, 100);

        await service.WithdrawAsync(AccountId, 100);

        var statement = await service.GetStatementAsync(AccountId);
        Assert.Equal(0, statement[0].Balance);
    }

    private async Task ClearOperationsAsync()
    {
        var client = new Client(_configuration["Supabase:Url"]!, _configuration["Supabase:Key"]!);

        await client.From<OperationRecord>()
            .Where(operation => operation.AccountId == AccountId)
            .Delete();
    }
}
