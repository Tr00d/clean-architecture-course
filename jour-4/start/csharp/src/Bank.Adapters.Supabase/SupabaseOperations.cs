using Bank.Domain.Models;
using Bank.Domain.Ports;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace Bank.Adapters.Supabase;

public class SupabaseOperations(string url, string key) : IOperations
{
    private readonly Client _client = new(url, key);

    public async Task<Account> AccountOfAsync(AccountId accountId)
    {
        var response = await _client.From<OperationRecord>()
            .Where(operation => operation.AccountId == accountId.Value)
            .Order(operation => operation.Date, Ordering.Ascending)
            .Order(operation => operation.Id, Ordering.Ascending)
            .Get();

        var history = response.Models
            .Select(record => new Operation(new Amount(record.Amount), record.Date.ToUniversalTime()))
            .ToList();

        return new Account(accountId, history);
    }

    public async Task SaveAsync(Account account)
    {
        var records = account.NewOperations
            .Select(operation => new OperationRecord
            {
                AccountId = account.Id.Value,
                Amount = operation.Amount.Value,
                Date = operation.Date
            })
            .ToList();

        await _client.From<OperationRecord>().Insert(records);
    }
}
