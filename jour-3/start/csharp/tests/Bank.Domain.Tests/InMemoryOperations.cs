using Bank.Domain.Models;
using Bank.Domain.Ports;

namespace Bank.Domain.Tests;

public class InMemoryOperations : IOperations
{
    private readonly Dictionary<AccountId, IReadOnlyList<Operation>> _operations = [];

    public Task<Account> AccountOfAsync(AccountId accountId)
    {
        var operations = _operations.GetValueOrDefault(accountId, []);
        return Task.FromResult(new Account(accountId, operations));
    }

    public Task SaveAsync(Account account)
    {
        _operations[account.Id] = [.. _operations.GetValueOrDefault(account.Id, []), .. account.NewOperations];
        return Task.CompletedTask;
    }
}
