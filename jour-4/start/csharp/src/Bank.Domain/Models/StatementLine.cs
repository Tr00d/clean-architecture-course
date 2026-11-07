namespace Bank.Domain.Models;

public record StatementLine(DateTime Date, Amount Amount, Amount Balance);
