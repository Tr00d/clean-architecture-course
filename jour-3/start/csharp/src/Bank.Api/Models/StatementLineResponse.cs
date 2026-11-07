namespace Bank.Api.Models;

public record StatementLineResponse(DateTime Date, decimal Amount, decimal Balance);
