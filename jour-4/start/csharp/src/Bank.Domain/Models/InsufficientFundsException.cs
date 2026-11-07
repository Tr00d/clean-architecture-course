namespace Bank.Domain.Models;

public class InsufficientFundsException(Amount balance)
    : WithdrawalRefusedException($"Insufficient funds: the balance is {balance.Value}.");
