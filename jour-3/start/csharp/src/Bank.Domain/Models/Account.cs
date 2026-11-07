namespace Bank.Domain.Models;

public class Account(AccountId id, IReadOnlyList<Operation> history)
{
    private readonly List<Operation> _newOperations = [];

    public AccountId Id => id;

    public IReadOnlyList<Operation> History => history;

    public IReadOnlyList<Operation> NewOperations => _newOperations;

    public Amount Balance => AllOperations.Aggregate(Amount.Zero, (balance, operation) => balance + operation.Amount);

    private IEnumerable<Operation> AllOperations => history.Concat(_newOperations);

    public void Deposit(Amount amount, DateTime date)
    {
        _newOperations.Add(new Operation(amount, date));
    }

    public void Withdraw(Amount amount, DateTime date)
    {
        if (amount > Balance)
        {
            throw new InsufficientFundsException(Balance);
        }

        _newOperations.Add(new Operation(-amount, date));
    }

    public IReadOnlyList<StatementLine> Statement()
    {
        var lines = new List<StatementLine>();
        var balance = Amount.Zero;

        foreach (var operation in AllOperations.OrderBy(operation => operation.Date))
        {
            balance += operation.Amount;
            lines.Add(new StatementLine(operation.Date, operation.Amount, balance));
        }

        lines.Reverse();
        return lines;
    }
}
