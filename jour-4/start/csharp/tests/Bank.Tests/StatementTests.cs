using System.Net.Http.Json;
using Bank.Adapters.Rest.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bank.Tests;

public class StatementTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Statement_ReturnsSeedOperationsNewestFirstWithRunningBalance()
    {
        var client = factory.CreateClient();

        var statement = await client.GetFromJsonAsync<List<StatementLineResponse>>("/accounts/1/operations", TestContext.Current.CancellationToken);

        StatementLineResponse[] expected =
        [
            new(new DateTime(2021, 1, 20, 12, 0, 0, DateTimeKind.Utc), -500, 2500),
            new(new DateTime(2021, 1, 15, 12, 0, 0, DateTimeKind.Utc), 2000, 3000),
            new(new DateTime(2021, 1, 10, 12, 0, 0, DateTimeKind.Utc), 1000, 1000)
        ];
        Assert.Equal(expected, statement);
    }
}
