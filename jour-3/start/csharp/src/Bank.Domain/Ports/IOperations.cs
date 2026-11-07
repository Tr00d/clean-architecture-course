using Bank.Domain.Models;

namespace Bank.Domain.Ports;

public interface IOperations
{
    Task<Account> AccountOfAsync(AccountId accountId);

    Task SaveAsync(Account account);
}
