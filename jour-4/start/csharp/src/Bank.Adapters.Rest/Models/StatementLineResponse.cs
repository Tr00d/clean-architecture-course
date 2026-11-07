namespace Bank.Adapters.Rest.Models;

public record StatementLineResponse(DateTime Date, decimal Amount, decimal Balance);
