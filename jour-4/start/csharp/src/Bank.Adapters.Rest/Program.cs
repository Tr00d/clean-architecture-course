using Bank.Adapters.Supabase;
using Bank.Adapters.Time;
using Bank.Domain.Ports;
using Bank.Domain.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.local.json");

builder.Services.AddControllers();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IOperations>(_ =>
    new SupabaseOperations(builder.Configuration["Supabase:Url"]!, builder.Configuration["Supabase:Key"]!));
builder.Services.AddScoped<IForDepositing, AccountService>();
builder.Services.AddScoped<IForWithdrawing, AccountService>();
builder.Services.AddScoped<IForPrintingStatements, AccountService>();

var app = builder.Build();

app.MapControllers();

app.Run();
