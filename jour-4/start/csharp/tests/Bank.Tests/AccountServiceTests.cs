using Bank.Adapters.Supabase;
using Bank.Adapters.Time;
using Bank.Domain.Models;
using Bank.Domain.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supabase;

namespace Bank.Tests;

public class AccountServiceTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private static readonly AccountId AccountId = new(42);

    private readonly IConfiguration _configuration = factory.Services.GetRequiredService<IConfiguration>();

    public async ValueTask InitializeAsync() => await ClearOperationsAsync();

    public async ValueTask DisposeAsync() => await ClearOperationsAsync();

    [Fact]
    public async Task Statement_IsEmpty_WhenTheAccountHasNoOperations()
    {
        var service = CreateService();

        var statement = await service.PrintStatementAsync(AccountId);

        Assert.Empty(statement);
    }

    [Fact]
    public async Task Statement_ListsOperationsNewestFirstWithRunningBalance()
    {
        var service = CreateService();
        await service.DepositAsync(AccountId, new Amount(1000));
        await service.DepositAsync(AccountId, new Amount(2000));
        await service.WithdrawAsync(AccountId, new Amount(500));

        var statement = await service.PrintStatementAsync(AccountId);

        Assert.Equal([-500m, 2000m, 1000m], statement.Select(line => line.Amount.Value));
        Assert.Equal([2500m, 3000m, 1000m], statement.Select(line => line.Balance.Value));
    }

    [Fact]
    public async Task Statement_DatesOperationsWithTheCurrentTime()
    {
        var service = CreateService();
        var before = DateTime.UtcNow;

        await service.DepositAsync(AccountId, new Amount(1000));

        var line = Assert.Single(await service.PrintStatementAsync(AccountId));
        Assert.InRange(line.Date, before.AddSeconds(-1), DateTime.UtcNow);
    }

    [Fact]
    public async Task Withdrawal_IsRefused_WhenAboveTheBalance()
    {
        var service = CreateService();
        await service.DepositAsync(AccountId, new Amount(100));

        await Assert.ThrowsAsync<InsufficientFundsException>(() => service.WithdrawAsync(AccountId, new Amount(100.01m)));

        var line = Assert.Single(await service.PrintStatementAsync(AccountId));
        Assert.Equal(new Amount(100), line.Balance);
    }

    [Fact]
    public async Task Withdrawal_IsAccepted_WhenEqualToTheBalance()
    {
        var service = CreateService();
        await service.DepositAsync(AccountId, new Amount(100));

        await service.WithdrawAsync(AccountId, new Amount(100));

        var statement = await service.PrintStatementAsync(AccountId);
        Assert.Equal(Amount.Zero, statement[0].Balance);
    }

    private AccountService CreateService()
    {
        var operations = new SupabaseOperations(_configuration["Supabase:Url"]!, _configuration["Supabase:Key"]!);

        return new AccountService(operations, new SystemClock());
    }

    private async Task ClearOperationsAsync()
    {
        var client = new Client(_configuration["Supabase:Url"]!, _configuration["Supabase:Key"]!);

        await client.From<OperationRecord>()
            .Where(operation => operation.AccountId == AccountId.Value)
            .Delete();
    }
}
