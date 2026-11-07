using Bank.Business;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.local.json");

builder.Services.AddControllers();
builder.Services.AddScoped<AccountService>();

var app = builder.Build();

app.MapControllers();

app.Run();
