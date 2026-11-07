using System.Globalization;
using Bank.Domain.Models;
using Bank.Domain.Ports;

namespace Bank.Adapters.Cli;

public class AtmScreen(
    AccountId accountId,
    IForDepositing depositing,
    IForWithdrawing withdrawing,
    IForPrintingStatements printingStatements,
    TextReader input,
    TextWriter output)
{
    public async Task RunAsync()
    {
        while (await input.ReadLineAsync() is { } line && line != "quit")
        {
            var command = line.Split(' ');

            switch (command[0])
            {
                case "deposit":
                    await depositing.DepositAsync(accountId, ParseAmount(command[1]));
                    break;
                case "withdraw":
                    await WithdrawAsync(ParseAmount(command[1]));
                    break;
                case "statement":
                    await PrintStatementAsync();
                    break;
            }
        }
    }

    private async Task WithdrawAsync(Amount amount)
    {
        try
        {
            await withdrawing.WithdrawAsync(accountId, amount);
        }
        catch (WithdrawalRefusedException exception)
        {
            await output.WriteLineAsync(exception.Message);
        }
    }

    private async Task PrintStatementAsync()
    {
        var statement = await printingStatements.PrintStatementAsync(accountId);

        foreach (var line in statement)
        {
            await output.WriteLineAsync($"{line.Date:yyyy-MM-dd} | {line.Amount.Value} | {line.Balance.Value}");
        }

        var balance = statement.FirstOrDefault()?.Balance ?? Amount.Zero;
        await output.WriteLineAsync($"Balance: {balance.Value}");
    }

    private static Amount ParseAmount(string value) => new(decimal.Parse(value, CultureInfo.InvariantCulture));
}
