using Bank.Domain.Models;

namespace Bank.Domain.Ports;

public interface IForPrintingStatements
{
    Task<IReadOnlyList<StatementLine>> PrintStatementAsync(AccountId accountId);
}
