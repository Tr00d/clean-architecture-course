namespace Bank.Domain.Models;

public class NightWithdrawalLimitExceededException(Amount limit)
    : WithdrawalRefusedException($"Night withdrawal limit exceeded: at most {limit.Value} can be withdrawn between 22:00 and 06:00.");
