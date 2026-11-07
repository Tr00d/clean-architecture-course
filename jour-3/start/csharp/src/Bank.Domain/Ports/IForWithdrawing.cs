using Bank.Domain.Models;

namespace Bank.Domain.Ports;

public interface IForWithdrawing
{
    Task WithdrawAsync(AccountId accountId, Amount amount);
}
