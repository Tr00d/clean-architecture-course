namespace Bank.Domain.Models;

public class InsufficientFundsException(Amount balance)
    : Exception($"Insufficient funds: the balance is {balance.Value}.");
