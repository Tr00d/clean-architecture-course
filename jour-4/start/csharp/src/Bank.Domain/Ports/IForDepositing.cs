using Bank.Domain.Models;

namespace Bank.Domain.Ports;

public interface IForDepositing
{
    Task DepositAsync(AccountId accountId, Amount amount);
}
