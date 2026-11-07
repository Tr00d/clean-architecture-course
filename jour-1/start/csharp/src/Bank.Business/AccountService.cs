using Bank.Data;
using Microsoft.Extensions.Configuration;

namespace Bank.Business;

public class AccountService(IConfiguration configuration)
{
    private readonly OperationsRepository _repository = new(configuration["Supabase:Url"]!, configuration["Supabase:Key"]!);

    public async Task<IReadOnlyList<StatementLine>> GetStatementAsync(int accountId)
    {
        var lines = new List<StatementLine>();
        var balance = 0m;

        foreach (var operation in await _repository.GetOperationsAsync(accountId))
        {
            balance += operation.Amount;
            lines.Add(new StatementLine(operation.Date.ToUniversalTime(), operation.Amount, balance));
        }

        lines.Reverse();
        return lines;
    }

    public async Task DepositAsync(int accountId, decimal amount)
    {
        await _repository.InsertOperationAsync(accountId, amount, DateTime.UtcNow);
    }

    public async Task WithdrawAsync(int accountId, decimal amount)
    {
        await _repository.InsertOperationAsync(accountId, -amount, DateTime.UtcNow);
    }
}
