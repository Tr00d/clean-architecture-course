using Bank.Domain.Ports;

namespace Bank.Adapters.Time;

public class SystemClock : IClock
{
    public DateTime Now() => DateTime.UtcNow;
}
