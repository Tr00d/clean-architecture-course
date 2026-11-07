namespace Bank.Domain.Models;

public abstract class WithdrawalRefusedException(string message) : Exception(message);
