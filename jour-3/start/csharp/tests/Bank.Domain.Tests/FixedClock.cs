using Bank.Domain.Ports;

namespace Bank.Domain.Tests;

public class FixedClock(DateTime now) : IClock
{
    public DateTime Now() => now;
}
