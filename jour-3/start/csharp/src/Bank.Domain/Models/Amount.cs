namespace Bank.Domain.Models;

public readonly record struct Amount(decimal Value)
{
    public static readonly Amount Zero = new(0);

    public static Amount operator +(Amount left, Amount right) => new(left.Value + right.Value);

    public static Amount operator -(Amount amount) => new(-amount.Value);

    public static bool operator >(Amount left, Amount right) => left.Value > right.Value;

    public static bool operator <(Amount left, Amount right) => left.Value < right.Value;
}
