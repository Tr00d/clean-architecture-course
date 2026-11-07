using Supabase;
using static Supabase.Postgrest.Constants;

namespace Bank.Data;

public class OperationsRepository(string url, string key)
{
    private readonly Client _client = new(url, key);

    public async Task<IReadOnlyList<OperationRecord>> GetOperationsAsync(int accountId)
    {
        var response = await _client.From<OperationRecord>()
            .Where(operation => operation.AccountId == accountId)
            .Order(operation => operation.Date, Ordering.Ascending)
            .Order(operation => operation.Id, Ordering.Ascending)
            .Get();

        return response.Models;
    }

    public async Task InsertOperationAsync(int accountId, decimal amount, DateTime date)
    {
        await _client.From<OperationRecord>().Insert(new OperationRecord
        {
            AccountId = accountId,
            Amount = amount,
            Date = date
        });
    }
}
