namespace Bank.Domain.Models;

public class WithdrawalLimitExceededException(Amount limit)
    : WithdrawalRefusedException($"Withdrawal limit exceeded: at most {limit.Value} can be withdrawn over 24 hours.");
