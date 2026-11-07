using Bank.Adapters.Cli;
using Bank.Adapters.Supabase;
using Bank.Adapters.Time;
using Bank.Domain.Models;
using Bank.Domain.Services;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.local.json")
    .Build();

var operations = new SupabaseOperations(configuration["Supabase:Url"]!, configuration["Supabase:Key"]!);
var service = new AccountService(operations, new SystemClock());

var screen = new AtmScreen(
    new AccountId(int.Parse(args[0])),
    service,
    service,
    service,
    Console.In,
    Console.Out);

Console.WriteLine("Commands: deposit <amount>, withdraw <amount>, statement, quit");
await screen.RunAsync();
