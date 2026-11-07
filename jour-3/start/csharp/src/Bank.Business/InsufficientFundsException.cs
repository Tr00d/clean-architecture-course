namespace Bank.Business;

public class InsufficientFundsException(decimal balance)
    : Exception($"Insufficient funds: the balance is {balance}.");
