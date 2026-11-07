namespace Bank.Domain.Models;

public class Account(AccountId id, IReadOnlyList<Operation> history)
{
    private static readonly Amount WithdrawalLimit = new(2500);
    private static readonly Amount NightWithdrawalLimit = new(500);

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

        if (WithdrawnInThe24HoursBefore(date) + amount > WithdrawalLimit)
        {
            throw new WithdrawalLimitExceededException(WithdrawalLimit);
        }

        if (IsNight(date) && amount > NightWithdrawalLimit)
        {
            throw new NightWithdrawalLimitExceededException(NightWithdrawalLimit);
        }

        _newOperations.Add(new Operation(-amount, date));
    }

    private Amount WithdrawnInThe24HoursBefore(DateTime date)
    {
        return AllOperations
            .Where(operation => operation.Amount < Amount.Zero && operation.Date > date.AddHours(-24))
            .Aggregate(Amount.Zero, (total, operation) => total + -operation.Amount);
    }

    private static bool IsNight(DateTime date) => date.Hour is >= 22 or < 6;

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
