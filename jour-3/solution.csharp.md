<!-- Généré depuis solution.src.md. Modifiez la source, pas ce fichier. -->

# Jour 3 : Solution (C#)

Ce guide reprend pas à pas la migration vers l'hexagone, les deux règles liées au temps et l'écran du DAB. Il part du projet de `jour-3/start/`.

## Le projet de départ

Ce projet a son propre `appsettings.local.json`, qui n'est pas versionné : si vous ne l'avez pas encore, suivez le README du projet pour créer `src/Bank.Api/appsettings.local.json`.

Démarrez la base, puis lancez les tests depuis `jour-3/start/csharp` :

```bash
dotnet test
```

Les 16 tests passent : les 10 tests du domaine, et les 6 tests du premier jour. Toutes les commandes de ce guide se lancent depuis ce dossier.

---

## Étape 1 : Brancher le domaine

On ne réécrit pas tout d'un coup. L'ancien code et le nouveau cohabitent pendant la migration, et les tests restent verts après chaque étape.

### Créer les adapters sortants

Le domaine a deux ports sortants : les opérations et l'horloge. Chacun a son adapter, dans son propre projet : l'horloge système n'a pas de lien avec Supabase, et l'écran du DAB en aura besoin lui aussi.

Créez les deux projets :

```bash
dotnet new classlib -n Bank.Adapters.Supabase -o src/Bank.Adapters.Supabase
dotnet new classlib -n Bank.Adapters.Time -o src/Bank.Adapters.Time
dotnet sln add src/Bank.Adapters.Supabase src/Bank.Adapters.Time
dotnet add src/Bank.Adapters.Supabase reference src/Bank.Domain
dotnet add src/Bank.Adapters.Supabase package Supabase --version 8.9.0
dotnet add src/Bank.Adapters.Time reference src/Bank.Domain
```

Supprimez le fichier `Class1.cs` de chaque projet.

Ces projets référencent le domaine : c'est le sens de dépendance de l'hexagone. `Bank.Domain` ne change pas, et ne référence toujours rien.

`src/Bank.Adapters.Time/SystemClock.cs`

```csharp
using Bank.Domain.Ports;

namespace Bank.Adapters.Time;

public class SystemClock : IClock
{
    public DateTime Now() => DateTime.UtcNow;
}
```

Copiez `OperationRecord` dans le nouveau projet, en changeant son namespace. L'identifiant de la ligne reste ici : il concerne la base, pas le domaine.

`src/Bank.Adapters.Supabase/OperationRecord.cs`

```csharp
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Bank.Adapters.Supabase;

[Table("operations")]
public class OperationRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("account_id")]
    public int AccountId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("date")]
    public DateTime Date { get; set; }
}
```

L'adapter traduit dans les deux sens. Il charge l'historique du compte et n'insère que `NewOperations` : c'est le compte qui sait ce qui est nouveau.

`src/Bank.Adapters.Supabase/SupabaseOperations.cs`

```csharp
using Bank.Domain.Models;
using Bank.Domain.Ports;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace Bank.Adapters.Supabase;

public class SupabaseOperations(string url, string key) : IOperations
{
    private readonly Client _client = new(url, key);

    public async Task<Account> AccountOfAsync(AccountId accountId)
    {
        var response = await _client.From<OperationRecord>()
            .Where(operation => operation.AccountId == accountId.Value)
            .Order(operation => operation.Date, Ordering.Ascending)
            .Order(operation => operation.Id, Ordering.Ascending)
            .Get();

        var history = response.Models
            .Select(record => new Operation(new Amount(record.Amount), record.Date.ToUniversalTime()))
            .ToList();

        return new Account(accountId, history);
    }

    public async Task SaveAsync(Account account)
    {
        var records = account.NewOperations
            .Select(operation => new OperationRecord
            {
                AccountId = account.Id.Value,
                Amount = operation.Amount.Value,
                Date = operation.Date
            })
            .ToList();

        await _client.From<OperationRecord>().Insert(records);
    }
}
```

Le SDK renvoie les dates en heure locale. `ToUniversalTime()`, qui était dans le service, passe dans l'adapter : le domaine n'a pas à connaître ce détail.

Lancez `dotnet test` : les 16 tests passent. Rien n'utilise encore les adapters.

### Faire passer les tests par le nouveau service

Ces tests vérifient les comportements de l'application depuis le premier jour. On les fait passer par l'`AccountService` du domaine et par la vraie base, sans changer ce qu'ils vérifient : ils deviennent les tests de l'adapter Supabase.

Ajoutez les références au projet de tests :

```bash
dotnet add tests/Bank.Tests reference src/Bank.Adapters.Supabase src/Bank.Adapters.Time
```

Remplacez `tests/Bank.Tests/AccountServiceTests.cs`. Chaque test crée toujours son service, et seuls les types changent :

```csharp
using Bank.Adapters.Supabase;
using Bank.Adapters.Time;
using Bank.Domain.Models;
using Bank.Domain.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supabase;

namespace Bank.Tests;

public class AccountServiceTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private static readonly AccountId AccountId = new(42);

    private readonly IConfiguration _configuration = factory.Services.GetRequiredService<IConfiguration>();

    public async ValueTask InitializeAsync() => await ClearOperationsAsync();

    public async ValueTask DisposeAsync() => await ClearOperationsAsync();

    [Fact]
    public async Task Statement_IsEmpty_WhenTheAccountHasNoOperations()
    {
        var service = CreateService();

        var statement = await service.PrintStatementAsync(AccountId);

        Assert.Empty(statement);
    }

    [Fact]
    public async Task Statement_ListsOperationsNewestFirstWithRunningBalance()
    {
        var service = CreateService();
        await service.DepositAsync(AccountId, new Amount(1000));
        await service.DepositAsync(AccountId, new Amount(2000));
        await service.WithdrawAsync(AccountId, new Amount(500));

        var statement = await service.PrintStatementAsync(AccountId);

        Assert.Equal([-500m, 2000m, 1000m], statement.Select(line => line.Amount.Value));
        Assert.Equal([2500m, 3000m, 1000m], statement.Select(line => line.Balance.Value));
    }

    [Fact]
    public async Task Statement_DatesOperationsWithTheCurrentTime()
    {
        var service = CreateService();
        var before = DateTime.UtcNow;

        await service.DepositAsync(AccountId, new Amount(1000));

        var line = Assert.Single(await service.PrintStatementAsync(AccountId));
        Assert.InRange(line.Date, before.AddSeconds(-1), DateTime.UtcNow);
    }

    [Fact]
    public async Task Withdrawal_IsRefused_WhenAboveTheBalance()
    {
        var service = CreateService();
        await service.DepositAsync(AccountId, new Amount(100));

        await Assert.ThrowsAsync<InsufficientFundsException>(() => service.WithdrawAsync(AccountId, new Amount(100.01m)));

        var line = Assert.Single(await service.PrintStatementAsync(AccountId));
        Assert.Equal(new Amount(100), line.Balance);
    }

    [Fact]
    public async Task Withdrawal_IsAccepted_WhenEqualToTheBalance()
    {
        var service = CreateService();
        await service.DepositAsync(AccountId, new Amount(100));

        await service.WithdrawAsync(AccountId, new Amount(100));

        var statement = await service.PrintStatementAsync(AccountId);
        Assert.Equal(Amount.Zero, statement[0].Balance);
    }

    private AccountService CreateService()
    {
        var operations = new SupabaseOperations(_configuration["Supabase:Url"]!, _configuration["Supabase:Key"]!);

        return new AccountService(operations, new SystemClock());
    }

    private async Task ClearOperationsAsync()
    {
        var client = new Client(_configuration["Supabase:Url"]!, _configuration["Supabase:Key"]!);

        await client.From<OperationRecord>()
            .Where(operation => operation.AccountId == AccountId.Value)
            .Delete();
    }
}
```

Les 16 tests passent : l'adapter Supabase se comporte comme l'ancien service. Le test de bout en bout passe toujours par l'ancien service, qui n'a pas changé.

### Brancher le contrôleur

C'est ici que l'application bascule, et que les ports entrants déclarés hier servent enfin. Le contrôleur devient un adapter entrant : il traduit le HTTP vers le vocabulaire du domaine, et ne dépend que des ports, pas du service qui les implémente.

Ajoutez les références à `Bank.Api` :

```bash
dotnet add src/Bank.Api reference src/Bank.Adapters.Supabase src/Bank.Adapters.Time
```

Remplacez `src/Bank.Api/Controllers/AccountsController.cs` :

```csharp
using Bank.Api.Models;
using Bank.Domain.Models;
using Bank.Domain.Ports;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Controllers;

[ApiController]
[Route("accounts/{accountId:int}")]
public class AccountsController(
    IForDepositing depositing,
    IForWithdrawing withdrawing,
    IForPrintingStatements printingStatements) : ControllerBase
{
    [HttpGet("operations")]
    public async Task<IReadOnlyList<StatementLineResponse>> GetStatement(int accountId)
    {
        var lines = await printingStatements.PrintStatementAsync(new AccountId(accountId));

        return lines
            .Select(line => new StatementLineResponse(line.Date, line.Amount.Value, line.Balance.Value))
            .ToList();
    }

    [HttpPost("deposits")]
    public async Task<IActionResult> Deposit(int accountId, AmountRequest request)
    {
        await depositing.DepositAsync(new AccountId(accountId), new Amount(request.Amount));
        return Created();
    }

    [HttpPost("withdrawals")]
    public async Task<IActionResult> Withdraw(int accountId, AmountRequest request)
    {
        try
        {
            await withdrawing.WithdrawAsync(new AccountId(accountId), new Amount(request.Amount));
            return Created();
        }
        catch (InsufficientFundsException exception)
        {
            return UnprocessableEntity(exception.Message);
        }
    }
}
```

`Program.cs` assemble l'application : c'est le seul endroit qui connaît à la fois les ports et ce qui les implémente. Remplacez-le :

```csharp
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
```

Les 16 tests passent. Le test de bout en bout traverse maintenant le contrôleur, les ports entrants, le domaine et l'adapter Supabase, sans avoir été modifié.

### Supprimer l'ancien service

Plus rien n'utilise les anciennes couches.

```bash
dotnet sln remove src/Bank.Business src/Bank.Data
dotnet remove src/Bank.Api reference src/Bank.Business/Bank.Business.csproj
```

Supprimez les dossiers `src/Bank.Business` et `src/Bank.Data`. Les 16 tests passent.

### Renommer le projet d'API

Cette étape est facultative, mais le nom dit maintenant ce que le projet est devenu : un adapter parmi d'autres.

Votre IDE peut faire ce renommage. À la main :

1. Retirez le projet de la solution : `dotnet sln remove src/Bank.Api`
2. Renommez le dossier en `src/Bank.Adapters.Rest`, et le fichier `Bank.Api.csproj` en `Bank.Adapters.Rest.csproj`
3. Supprimez ses dossiers `bin` et `obj`, puis ajoutez-le à la solution : `dotnet sln add src/Bank.Adapters.Rest`
4. Remplacez `Bank.Api` par `Bank.Adapters.Rest` dans les namespaces du projet, dans `tests/Bank.Tests/StatementTests.cs` et dans `tests/Bank.Tests/Bank.Tests.csproj`

Le fichier `appsettings.local.json` suit le dossier. Les 16 tests passent. Vérifiez à la main que le refus de retrait fonctionne toujours, avec l'API démarrée :

```bash
dotnet run --project src/Bank.Adapters.Rest
```

```bash
curl -X POST http://localhost:5122/accounts/1/withdrawals \
  -H "Content-Type: application/json" \
  -d '{ "amount": 100000 }'
```

L'API répond `422` avec le message de l'exception.

---

## Étape 2 : Des règles qui dépendent du temps

Chaque mob implémente une règle. Ce guide présente les deux à la suite : si vous n'en avez fait qu'une, le nombre de tests sera différent.

Les deux règles vivent dans le compte. Le retrait y reçoit déjà la date : pour tester "24 heures plus tard" ou "à 23 heures", il suffit de passer une autre date. Aucune attente, aucune base, et même pas besoin de l'horloge : elle ne sert qu'au service.

Tous les tests de cette étape vont dans `AccountTests`.

### Première règle : la limite sur 24 heures

#### Un retrait au-dessus de la limite est refusé

**Rouge**

Pour que le test compile, créez `src/Bank.Domain/Models/WithdrawalLimitExceededException.cs` :

```csharp
namespace Bank.Domain.Models;

public class WithdrawalLimitExceededException(Amount limit)
    : Exception($"Withdrawal limit exceeded: at most {limit.Value} can be withdrawn over 24 hours.");
```

Ajoutez ce test :

```csharp
    [Fact]
    public void Withdrawal_IsRefused_WhenAboveTheLimit()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(5000), Date);

        Assert.Throws<WithdrawalLimitExceededException>(() => account.Withdraw(new Amount(2500.01m), Date));

        Assert.Equal(new Amount(5000), account.Balance);
    }
```

Le test échoue, car aucune exception n'est levée.

**Vert**

Ajoutez à `Account` :

```csharp
    private static readonly Amount WithdrawalLimit = new(2500);
```

Et, dans `Withdraw`, après la vérification du solde :

```csharp
        if (amount > WithdrawalLimit)
        {
            throw new WithdrawalLimitExceededException(WithdrawalLimit);
        }
```

Les 11 tests du domaine passent.

#### Les retraits s'additionnent

**Rouge**

```csharp
    [Fact]
    public void Withdrawal_IsRefused_WhenWithdrawalsAddUpAboveTheLimit()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(5000), Date);
        account.Withdraw(new Amount(1500), Date);

        Assert.Throws<WithdrawalLimitExceededException>(() => account.Withdraw(new Amount(1500), Date.AddHours(1)));
    }
```

Le test échoue : chaque retrait est sous la limite, pris séparément.

**Vert**

Ajoutez à `Account` :

```csharp
    private Amount Withdrawn()
    {
        return AllOperations
            .Where(operation => operation.Amount < Amount.Zero)
            .Aggregate(Amount.Zero, (total, operation) => total + -operation.Amount);
    }
```

Et remplacez la condition dans `Withdraw` :

```csharp
        if (Withdrawn() + amount > WithdrawalLimit)
```

Les 12 tests du domaine passent.

#### La limite porte sur 24 heures glissantes

**Rouge**

```csharp
    [Fact]
    public void Withdrawal_IsAccepted_WhenPreviousWithdrawalsAre24HoursOld()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(5000), Date);
        account.Withdraw(new Amount(1500), Date);

        account.Withdraw(new Amount(1500), Date.AddHours(24));

        Assert.Equal(new Amount(2000), account.Balance);
    }
```

Le test échoue : tous les retraits comptent, quelle que soit leur date.

**Vert**

Remplacez `Withdrawn` dans `Account` :

```csharp
    private Amount WithdrawnInThe24HoursBefore(DateTime date)
    {
        return AllOperations
            .Where(operation => operation.Amount < Amount.Zero && operation.Date > date.AddHours(-24))
            .Aggregate(Amount.Zero, (total, operation) => total + -operation.Amount);
    }
```

Et la condition dans `Withdraw` :

```csharp
        if (WithdrawnInThe24HoursBefore(date) + amount > WithdrawalLimit)
```

Les 13 tests du domaine passent.

#### Un retrait égal à la limite est accepté

Ce cas est déjà couvert par le premier cycle : le test passera du premier coup, et il ne pilote aucun code. On sort donc du TDD le temps d'un test, pour fixer ce comportement à la limite.

```csharp
    [Fact]
    public void Withdrawal_IsAccepted_WhenEqualToTheLimit()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(5000), Date);

        account.Withdraw(new Amount(2500), Date);

        Assert.Equal(new Amount(2500), account.Balance);
    }
```

Un test qui n'a jamais échoué ne prouve rien. Vérifiez qu'il peut échouer en remplaçant temporairement la condition par `!(WithdrawalLimit > WithdrawnInThe24HoursBefore(date) + amount)` : le test échoue. Remettez la condition. Les 14 tests du domaine passent.

### Deuxième règle : la limite de nuit

#### Un retrait au-dessus de la limite de nuit est refusé dès 22 heures

**Rouge**

Pour que le test compile, créez `src/Bank.Domain/Models/NightWithdrawalLimitExceededException.cs` :

```csharp
namespace Bank.Domain.Models;

public class NightWithdrawalLimitExceededException(Amount limit)
    : Exception($"Night withdrawal limit exceeded: at most {limit.Value} can be withdrawn between 22:00 and 06:00.");
```

Ajoutez ce test :

```csharp
    [Fact]
    public void Withdrawal_IsRefused_WhenAboveTheNightLimit_AtNightfall()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(1000), Date);
        var nightfall = new DateTime(2021, 1, 10, 22, 0, 0, DateTimeKind.Utc);

        Assert.Throws<NightWithdrawalLimitExceededException>(() => account.Withdraw(new Amount(500.01m), nightfall));

        Assert.Equal(new Amount(1000), account.Balance);
    }
```

Le test échoue, car aucune exception n'est levée.

**Vert**

Ajoutez à `Account` :

```csharp
    private static readonly Amount NightWithdrawalLimit = new(500);
```

Et, dans `Withdraw`, juste avant d'ajouter l'opération :

```csharp
        if (date.Hour >= 22 && amount > NightWithdrawalLimit)
        {
            throw new NightWithdrawalLimitExceededException(NightWithdrawalLimit);
        }
```

Les 15 tests du domaine passent.

#### La nuit continue après minuit

**Rouge**

```csharp
    [Fact]
    public void Withdrawal_IsRefused_WhenAboveTheNightLimit_BeforeDawn()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(1000), Date);
        var beforeDawn = new DateTime(2021, 1, 10, 5, 59, 0, DateTimeKind.Utc);

        Assert.Throws<NightWithdrawalLimitExceededException>(() => account.Withdraw(new Amount(500.01m), beforeDawn));
    }
```

Le test échoue : à 5h59, l'heure n'est pas supérieure à 22.

**Vert**

Ajoutez à `Account` :

```csharp
    private static bool IsNight(DateTime date) => date.Hour is >= 22 or < 6;
```

Et remplacez la condition dans `Withdraw` :

```csharp
        if (IsNight(date) && amount > NightWithdrawalLimit)
```

Le nouveau test passe, mais 3 tests de la limite sur 24 heures échouent : `Date` vaut minuit dans `AccountTests`, et ces retraits ont maintenant lieu en pleine nuit. La règle est bonne, c'est la donnée de test qui ne convient plus. Placez `Date` à midi :

```csharp
    private static readonly DateTime Date = new(2021, 1, 10, 12, 0, 0, DateTimeKind.Utc);
```

Les 16 tests du domaine passent.

#### À 6 heures, la nuit est finie

Ce cas est déjà couvert par le cycle précédent : le test passera du premier coup, et il ne pilote aucun code. On sort donc du TDD le temps d'un test, pour fixer ce comportement à la limite.

```csharp
    [Fact]
    public void Withdrawal_IsAccepted_AboveTheNightLimit_AtDawn()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(1000), Date);
        var dawn = new DateTime(2021, 1, 10, 6, 0, 0, DateTimeKind.Utc);

        account.Withdraw(new Amount(600), dawn);

        Assert.Equal(new Amount(400), account.Balance);
    }
```

Un test qui n'a jamais échoué ne prouve rien. Vérifiez qu'il peut échouer en remplaçant temporairement `< 6` par `<= 6` : le test échoue. Remettez `< 6`. Les 17 tests du domaine passent.

#### 22 heures, dans quel fuseau ?

L'horloge renvoie de l'UTC : ici, la nuit commence à 22 heures UTC, soit à 23 heures à Paris en hiver. Une banque raisonne en heure locale. Pour corriger cela, le domaine devrait connaître le fuseau de la banque : une règle métier de plus, qui se teste de la même façon. On s'arrête ici pour ce guide, mais la question mérite d'être posée pendant la présentation.

### Traduire le refus dans l'API

Le contrôleur ne connaît que le refus pour solde insuffisant : un retrait au-dessus d'une limite provoquerait une erreur 500. Plutôt que de traiter chaque règle à part, les refus partagent un parent : le contrôleur n'a pas à connaître chaque raison de refus.

Créez `src/Bank.Domain/Models/WithdrawalRefusedException.cs` :

```csharp
namespace Bank.Domain.Models;

public abstract class WithdrawalRefusedException(string message) : Exception(message);
```

Remplacez le parent des exceptions :

```csharp
namespace Bank.Domain.Models;

public class InsufficientFundsException(Amount balance)
    : WithdrawalRefusedException($"Insufficient funds: the balance is {balance.Value}.");
```

```csharp
namespace Bank.Domain.Models;

public class WithdrawalLimitExceededException(Amount limit)
    : WithdrawalRefusedException($"Withdrawal limit exceeded: at most {limit.Value} can be withdrawn over 24 hours.");
```

```csharp
namespace Bank.Domain.Models;

public class NightWithdrawalLimitExceededException(Amount limit)
    : WithdrawalRefusedException($"Night withdrawal limit exceeded: at most {limit.Value} can be withdrawn between 22:00 and 06:00.");
```

Dans `AccountsController`, remplacez le `catch` :

```csharp
        catch (WithdrawalRefusedException exception)
```

Vérifiez-le à la main, avec l'API démarrée. Le compte 1 contient 2500 : déposez 1000, puis retirez 2501.

```bash
curl -X POST http://localhost:5122/accounts/1/deposits \
  -H "Content-Type: application/json" \
  -d '{ "amount": 1000 }'

curl -X POST http://localhost:5122/accounts/1/withdrawals \
  -H "Content-Type: application/json" \
  -d '{ "amount": 2501 }'
```

L'API répond `422` avec le message de la limite, ou de la limite de nuit si vous êtes entre 22 heures et 6 heures UTC. `supabase db reset` remet la base dans son état initial.

Lancez `dotnet test` : les 23 tests passent.

### Et avec des Seams ?

Au premier jour, il aurait fallu reprendre les Seams de la deuxième partie de l'étape 3, et simuler dans une sous-classe de test le stockage et l'heure de chaque opération. Ici, il n'y en a aucun, pour aucune des deux règles : l'heure et les données sont déjà à l'extérieur du domaine.

---

## Étape 3 : Un nouveau point d'entrée, l'écran du DAB

Un adapter entrant de plus, branché sur les mêmes ports que le contrôleur. Les tests pilotent l'écran avec les adapters en mémoire : pas de base, et pas de console à simuler.

L'écran lit et écrit dans un `TextReader` et un `TextWriter`.

Créez les deux projets :

```bash
dotnet new console -n Bank.Adapters.Cli -o src/Bank.Adapters.Cli
dotnet sln add src/Bank.Adapters.Cli
dotnet add src/Bank.Adapters.Cli reference src/Bank.Domain src/Bank.Adapters.Supabase src/Bank.Adapters.Time
dotnet add src/Bank.Adapters.Cli package Microsoft.Extensions.Configuration.Json --version 10.0.14
```

Créez `tests/Bank.Adapters.Cli.Tests/Bank.Adapters.Cli.Tests.csproj`. Il réutilise les adapters en mémoire du domaine :

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\src\Bank.Adapters.Cli\Bank.Adapters.Cli.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include="..\Bank.Domain.Tests\InMemoryOperations.cs" Link="InMemoryOperations.cs" />
    <Compile Include="..\Bank.Domain.Tests\FixedClock.cs" Link="FixedClock.cs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />
    <PackageReference Include="xunit.v3" Version="4.0.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="4.0.1" />
  </ItemGroup>

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

```bash
dotnet sln add tests/Bank.Adapters.Cli.Tests
```

### Déposer

**Rouge**

Créez `src/Bank.Adapters.Cli/AtmScreen.cs`, sans aucune logique pour l'instant. Le compilateur signale des paramètres non lus : ils serviront dans les prochains cycles.

```csharp
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
    }
}
```

Remplacez `src/Bank.Adapters.Cli/Program.cs` :

```csharp
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
```

Comme `Program.cs` dans l'API, c'est le seul endroit qui connaît le service : `AccountService` implémente les trois ports, et il les remplit tous les trois.

Créez `tests/Bank.Adapters.Cli.Tests/AtmScreenTests.cs` :

```csharp
using Bank.Domain.Models;
using Bank.Domain.Services;
using Bank.Domain.Tests;
using Xunit;

namespace Bank.Adapters.Cli.Tests;

public class AtmScreenTests
{
    private static readonly AccountId Id = new(1);
    private static readonly DateTime Now = new(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOperations _operations = new();

    [Fact]
    public async Task Deposit_IncreasesTheBalance()
    {
        await RunAsync("deposit 100");

        var account = await _operations.AccountOfAsync(Id);
        Assert.Equal(new Amount(100), account.Balance);
    }

    private async Task<string> RunAsync(params string[] commands)
    {
        var input = new StringReader(string.Join(Environment.NewLine, commands));
        var output = new StringWriter();
        var service = new AccountService(_operations, new FixedClock(Now));
        var screen = new AtmScreen(Id, service, service, service, input, output);

        await screen.RunAsync();

        return output.ToString();
    }
}
```

Le test échoue : le solde vaut 0.

**Vert**

Remplacez `AtmScreen` :

```csharp
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
            }
        }
    }

    private static Amount ParseAmount(string value) => new(decimal.Parse(value, CultureInfo.InvariantCulture));
}
```

Le test passe.

### Retirer

**Rouge**

Ajoutez ce test à `AtmScreenTests` :

```csharp
    [Fact]
    public async Task Withdrawal_DecreasesTheBalance()
    {
        await RunAsync("deposit 100", "withdraw 30");

        var account = await _operations.AccountOfAsync(Id);
        Assert.Equal(new Amount(70), account.Balance);
    }
```

Le test échoue : le solde vaut 100.

**Vert**

Ajoutez ce cas au `switch` :

```csharp
                case "withdraw":
                    await withdrawing.WithdrawAsync(accountId, ParseAmount(command[1]));
                    break;
```

Les 2 tests passent.

### Afficher un refus

**Rouge**

```csharp
    [Fact]
    public async Task Withdrawal_DisplaysTheReason_WhenRefused()
    {
        var output = await RunAsync("withdraw 30");

        Assert.Contains("Insufficient funds", output);
    }
```

Le test échoue : l'exception n'est pas interceptée.

**Vert**

Remplacez le cas `withdraw` :

```csharp
                case "withdraw":
                    await WithdrawAsync(ParseAmount(command[1]));
                    break;
```

Et ajoutez à `AtmScreen` :

```csharp
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
```

Les 3 tests passent. Grâce à `WithdrawalRefusedException`, l'écran affiche aussi les refus des deux limites.

### Afficher le relevé et le solde

**Rouge**

```csharp
    [Fact]
    public async Task Statement_DisplaysTheOperationsAndTheBalance()
    {
        var output = await RunAsync("deposit 100", "statement");

        Assert.Contains("2021-01-10 | 100 | 100", output);
        Assert.Contains("Balance: 100", output);
    }
```

Le test échoue : l'écran n'affiche rien.

**Vert**

Ajoutez ce cas au `switch` :

```csharp
                case "statement":
                    await PrintStatementAsync();
                    break;
```

Et ajoutez à `AtmScreen` :

```csharp
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
```

Les 4 tests passent.

### Utiliser l'écran

L'écran a besoin de la même configuration que l'API. Copiez `src/Bank.Adapters.Rest/appsettings.local.json` dans `src/Bank.Adapters.Cli`, et ajoutez à `Bank.Adapters.Cli.csproj` :

```xml
  <ItemGroup>
    <None Update="appsettings.local.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

Lancez l'écran sur le compte 1 :

```bash
dotnet run --project src/Bank.Adapters.Cli -- 1
```

```text
Commands: deposit <amount>, withdraw <amount>, statement, quit
statement
2021-01-20 | -500 | 2500
2021-01-15 | 2000 | 3000
2021-01-10 | 1000 | 1000
Balance: 2500
quit
```

Lancez `dotnet test` : les 27 tests passent.

Combien de lignes du domaine avez-vous modifiées ? Aucune : l'écran appelle les mêmes ports entrants que le contrôleur.

---

## Pour aller plus loin : Un contrat pour les adapters

`InMemoryOperations` remplace la vraie base dans la plupart des tests. S'il ne se comporte pas comme `SupabaseOperations`, ces tests passent pour de mauvaises raisons. Un test de contrat écrit une seule fois ce qu'on attend d'un port, et s'exécute sur chacun de ses adapters.

**Rouge**

Dans `tests/Bank.Tests/Bank.Tests.csproj`, reliez l'adapter en mémoire :

```xml
  <ItemGroup>
    <Compile Include="..\Bank.Domain.Tests\InMemoryOperations.cs" Link="InMemoryOperations.cs" />
  </ItemGroup>
```

Créez `tests/Bank.Tests/OperationsContract.cs`. La classe est abstraite : xUnit exécute ses tests sur chaque classe qui en hérite.

```csharp
using Bank.Domain.Models;
using Bank.Domain.Ports;

namespace Bank.Tests;

public abstract class OperationsContract
{
    private static readonly DateTime FirstDate = new(2021, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SecondDate = new(2021, 1, 11, 12, 0, 0, DateTimeKind.Utc);

    protected abstract AccountId AccountId { get; }

    protected abstract IOperations Operations { get; }

    [Fact]
    public async Task AnAccountWithoutOperations_HasAnEmptyHistory()
    {
        var account = await Operations.AccountOfAsync(AccountId);

        Assert.Empty(account.History);
    }

    [Fact]
    public async Task SavedOperations_AreInTheHistory()
    {
        await DepositAsync(new Amount(100), FirstDate);

        var account = await Operations.AccountOfAsync(AccountId);

        Assert.Equal(new Operation(new Amount(100), FirstDate), Assert.Single(account.History));
    }

    [Fact]
    public async Task Saving_OnlyAddsTheNewOperations()
    {
        await DepositAsync(new Amount(100), FirstDate);
        await DepositAsync(new Amount(50), SecondDate);

        var account = await Operations.AccountOfAsync(AccountId);

        Assert.Equal([new Amount(100), new Amount(50)], account.History.Select(operation => operation.Amount));
    }

    [Fact]
    public async Task History_IsSortedByDate()
    {
        await DepositAsync(new Amount(50), SecondDate);
        await DepositAsync(new Amount(100), FirstDate);

        var account = await Operations.AccountOfAsync(AccountId);

        Assert.Equal([FirstDate, SecondDate], account.History.Select(operation => operation.Date));
    }

    private async Task DepositAsync(Amount amount, DateTime date)
    {
        var account = await Operations.AccountOfAsync(AccountId);
        account.Deposit(amount, date);
        await Operations.SaveAsync(account);
    }
}
```

`tests/Bank.Tests/InMemoryOperationsContractTests.cs`

```csharp
using Bank.Domain.Models;
using Bank.Domain.Ports;
using Bank.Domain.Tests;

namespace Bank.Tests;

public class InMemoryOperationsContractTests : OperationsContract
{
    protected override AccountId AccountId => new(1);

    protected override IOperations Operations { get; } = new InMemoryOperations();
}
```

`tests/Bank.Tests/SupabaseOperationsContractTests.cs`

```csharp
using Bank.Adapters.Supabase;
using Bank.Domain.Models;
using Bank.Domain.Ports;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supabase;

namespace Bank.Tests;

public class SupabaseOperationsContractTests(WebApplicationFactory<Program> factory)
    : OperationsContract, IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly IConfiguration _configuration = factory.Services.GetRequiredService<IConfiguration>();

    protected override AccountId AccountId => new(43);

    protected override IOperations Operations =>
        new SupabaseOperations(_configuration["Supabase:Url"]!, _configuration["Supabase:Key"]!);

    public async ValueTask InitializeAsync() => await ClearOperationsAsync();

    public async ValueTask DisposeAsync() => await ClearOperationsAsync();

    private async Task ClearOperationsAsync()
    {
        var client = new Client(_configuration["Supabase:Url"]!, _configuration["Supabase:Key"]!);

        await client.From<OperationRecord>()
            .Where(operation => operation.AccountId == AccountId.Value)
            .Delete();
    }
}
```

`History_IsSortedByDate` échoue, uniquement sur l'adapter en mémoire : Supabase trie les opérations, pas lui.

Le test a trouvé une vraie différence entre les deux adapters. Elle ne se voyait pas, car le relevé trie les opérations lui-même.

**Vert**

Dans `InMemoryOperations`, remplacez `AccountOfAsync` :

```csharp
    public Task<Account> AccountOfAsync(AccountId accountId)
    {
        var operations = _operations.GetValueOrDefault(accountId, [])
            .OrderBy(operation => operation.Date)
            .ToList();

        return Task.FromResult(new Account(accountId, operations));
    }
```

Les 35 tests passent : les 27 tests précédents, et les 4 tests du contrat sur chacun des 2 adapters.

Un nouvel adapter, par exemple le stockage dans un fichier JSON du défi final, n'aura qu'à rejoindre ce contrat.
