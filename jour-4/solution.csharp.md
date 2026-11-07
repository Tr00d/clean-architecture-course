<!-- Généré depuis solution.src.md. Modifiez la source, pas ce fichier. -->

# Jour 4 : Solution (C#)

Ce guide reprend pas à pas le passage de l'hexagone à la Clean Architecture, puis les tests d'architecture. Il part du projet de `jour-4/start/`.

## Le projet de départ

Ce projet a ses propres fichiers `appsettings.local.json`, qui ne sont pas versionnés : si vous ne les avez pas encore, suivez le README du projet pour créer ceux de `src/Bank.Adapters.Rest` et de `src/Bank.Adapters.Cli`.

Démarrez la base, puis lancez les tests depuis `jour-4/start/csharp` :

```bash
dotnet test
```

Les 27 tests passent. Toutes les commandes de ce guide se lancent depuis ce dossier.

---

## Étape 1 : Séparer l'application du domaine

C'est un refactoring : aucun comportement ne change, et les tests existants servent de filet. C'est pourquoi cette étape ne suit pas le cycle Rouge, Vert : aucune règle nouvelle ne pilote le code.

### Créer le projet d'application

```bash
dotnet new classlib -n Bank.Application -o src/Bank.Application
dotnet sln add src/Bank.Application
dotnet add src/Bank.Application reference src/Bank.Domain
```

Supprimez `Class1.cs`.

### Déplacer les ports et le service

Les ports entrants décrivent ce que propose l'application, et les ports sortants ce dont elle a besoin : ils appartiennent au cercle Use Cases. Le domaine ne garde que les entités et leurs règles.

1. Déplacez les dossiers `Ports` et `Services` de `src/Bank.Domain` vers `src/Bank.Application`
2. Remplacez partout `Bank.Domain.Ports` par `Bank.Application.Ports`, et `Bank.Domain.Services` par `Bank.Application.Services`
3. Faites pointer les adapters vers le projet d'application :

```bash
dotnet remove src/Bank.Adapters.Supabase reference ../Bank.Domain/Bank.Domain.csproj
dotnet remove src/Bank.Adapters.Time reference ../Bank.Domain/Bank.Domain.csproj
dotnet remove src/Bank.Adapters.Cli reference ../Bank.Domain/Bank.Domain.csproj
dotnet add src/Bank.Adapters.Supabase reference src/Bank.Application
dotnet add src/Bank.Adapters.Time reference src/Bank.Application
dotnet add src/Bank.Adapters.Cli reference src/Bank.Application
dotnet add src/Bank.Adapters.Rest reference src/Bank.Application
```

Les tests suivent le même découpage. Créez `tests/Bank.Application.Tests/Bank.Application.Tests.csproj` :

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\src\Bank.Application\Bank.Application.csproj" />
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
dotnet sln add tests/Bank.Application.Tests
```

1. Déplacez `AccountServiceTests.cs`, `InMemoryOperations.cs` et `FixedClock.cs` de `tests/Bank.Domain.Tests` vers `tests/Bank.Application.Tests`, et changez leur namespace en `Bank.Application.Tests`
2. Dans `tests/Bank.Adapters.Cli.Tests`, faites pointer les deux fichiers liés du `.csproj` vers `..\Bank.Application.Tests\`, et remplacez le `using Bank.Domain.Tests` par `using Bank.Application.Tests`

Les 27 tests passent.

### Un dossier par cas d'utilisation

Le service disparaît. Chaque cas d'utilisation reçoit son propre dossier, avec tout ce qui lui est propre : on regroupe le code par ce qu'il fait, et non par ce qu'il est. En ouvrant le projet d'application, on lit ce qu'elle fait, et travailler sur le relevé ne demande d'ouvrir qu'un seul dossier.

```text
Bank.Application/
├── Deposit/          # IForDepositing, DepositUseCase
├── PrintStatement/   # IForPrintingStatements, PrintStatementUseCase
├── Withdraw/         # IForWithdrawing, WithdrawUseCase
└── Ports/            # IOperations, IClock : partagés par plusieurs cas d'utilisation
```

Déplacez chaque port entrant dans le dossier de son cas d'utilisation, et changez son namespace en `Bank.Application.Deposit`, `Bank.Application.Withdraw` ou `Bank.Application.PrintStatement`.

Les classes portent le suffixe `UseCase` : en C#, une classe `Deposit` dans un namespace `Bank.Application.Deposit` entrerait en conflit avec lui.

`src/Bank.Application/Deposit/DepositUseCase.cs`

```csharp
using Bank.Application.Ports;
using Bank.Domain.Models;

namespace Bank.Application.Deposit;

public class DepositUseCase(IOperations operations, IClock clock) : IForDepositing
{
    public async Task DepositAsync(AccountId accountId, Amount amount)
    {
        var account = await operations.AccountOfAsync(accountId);
        account.Deposit(amount, clock.Now());
        await operations.SaveAsync(account);
    }
}
```

`src/Bank.Application/Withdraw/WithdrawUseCase.cs`

```csharp
using Bank.Application.Ports;
using Bank.Domain.Models;

namespace Bank.Application.Withdraw;

public class WithdrawUseCase(IOperations operations, IClock clock) : IForWithdrawing
{
    public async Task WithdrawAsync(AccountId accountId, Amount amount)
    {
        var account = await operations.AccountOfAsync(accountId);
        account.Withdraw(amount, clock.Now());
        await operations.SaveAsync(account);
    }
}
```

`src/Bank.Application/PrintStatement/PrintStatementUseCase.cs`

```csharp
using Bank.Application.Ports;
using Bank.Domain.Models;

namespace Bank.Application.PrintStatement;

public class PrintStatementUseCase(IOperations operations) : IForPrintingStatements
{
    public async Task<IReadOnlyList<StatementLine>> PrintStatementAsync(AccountId accountId)
    {
        var account = await operations.AccountOfAsync(accountId);
        return account.Statement();
    }
}
```

Supprimez le dossier `src/Bank.Application/Services`.

Dans `src/Bank.Adapters.Rest/Program.cs`, remplacez le `using Bank.Application.Services` par les namespaces des trois cas d'utilisation :

```csharp
using Bank.Application.Deposit;
using Bank.Application.PrintStatement;
using Bank.Application.Withdraw;
```

Et les trois enregistrements :

```csharp
builder.Services.AddScoped<IForDepositing, DepositUseCase>();
builder.Services.AddScoped<IForWithdrawing, WithdrawUseCase>();
builder.Services.AddScoped<IForPrintingStatements, PrintStatementUseCase>();
```

Dans `src/Bank.Adapters.Cli/Program.cs`, faites le même remplacement de `using`, puis :

```csharp
var clock = new SystemClock();

var screen = new AtmScreen(
    new AccountId(int.Parse(args[0])),
    new DepositUseCase(operations, clock),
    new WithdrawUseCase(operations, clock),
    new PrintStatementUseCase(operations),
    Console.In,
    Console.Out);
```

`AtmScreen` et `AccountsController` ne changent que par leurs `using` : ils ne connaissent que les ports. C'est ce qui rend ce découpage aussi peu coûteux.

Il reste les tests, qui suivent le même découpage. Supprimez `tests/Bank.Application.Tests/AccountServiceTests.cs`, et créez :

`tests/Bank.Application.Tests/Deposit/DepositTests.cs`

```csharp
using Bank.Application.Deposit;
using Bank.Domain.Models;
using Xunit;

namespace Bank.Application.Tests.Deposit;

public class DepositTests
{
    private static readonly AccountId Id = new(1);
    private static readonly DateTime Now = new(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOperations _operations = new();
    private readonly IForDepositing _depositing;

    public DepositTests()
    {
        _depositing = new DepositUseCase(_operations, new FixedClock(Now));
    }

    [Fact]
    public async Task Deposit_SavesTheOperationAtTheCurrentDate()
    {
        await _depositing.DepositAsync(Id, new Amount(100));

        var account = await _operations.AccountOfAsync(Id);
        var operation = Assert.Single(account.History);
        Assert.Equal(new Operation(new Amount(100), Now), operation);
    }
}
```

`tests/Bank.Application.Tests/Withdraw/WithdrawTests.cs`

```csharp
using Bank.Application.Deposit;
using Bank.Application.Withdraw;
using Bank.Domain.Models;
using Xunit;

namespace Bank.Application.Tests.Withdraw;

public class WithdrawTests
{
    private static readonly AccountId Id = new(1);
    private static readonly DateTime Now = new(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOperations _operations = new();
    private readonly IForDepositing _depositing;
    private readonly IForWithdrawing _withdrawing;

    public WithdrawTests()
    {
        var clock = new FixedClock(Now);
        _depositing = new DepositUseCase(_operations, clock);
        _withdrawing = new WithdrawUseCase(_operations, clock);
    }

    [Fact]
    public async Task Withdraw_SavesTheOperationAtTheCurrentDate()
    {
        await _depositing.DepositAsync(Id, new Amount(100));

        await _withdrawing.WithdrawAsync(Id, new Amount(30));

        var account = await _operations.AccountOfAsync(Id);
        Assert.Equal(new Operation(new Amount(-30), Now), account.History[^1]);
    }
}
```

`tests/Bank.Application.Tests/PrintStatement/PrintStatementTests.cs`

```csharp
using Bank.Application.Deposit;
using Bank.Application.PrintStatement;
using Bank.Domain.Models;
using Xunit;

namespace Bank.Application.Tests.PrintStatement;

public class PrintStatementTests
{
    private static readonly AccountId Id = new(1);
    private static readonly DateTime Now = new(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOperations _operations = new();
    private readonly IForDepositing _depositing;
    private readonly IForPrintingStatements _printingStatements;

    public PrintStatementTests()
    {
        _depositing = new DepositUseCase(_operations, new FixedClock(Now));
        _printingStatements = new PrintStatementUseCase(_operations);
    }

    [Fact]
    public async Task PrintStatement_ReturnsTheStatementOfTheAccount()
    {
        await _depositing.DepositAsync(Id, new Amount(100));

        var statement = await _printingStatements.PrintStatementAsync(Id);

        var line = Assert.Single(statement);
        Assert.Equal(new StatementLine(Now, new Amount(100), new Amount(100)), line);
    }
}
```

Renommez `tests/Bank.Tests/AccountServiceTests.cs` en `UseCasesWithSupabaseTests.cs`. Le début de la classe change, `CreateService` disparaît, et chaque `service.` devient l'appel au cas d'utilisation concerné, par exemple `Deposit.DepositAsync(...)` :

```csharp
using Bank.Adapters.Supabase;
using Bank.Adapters.Time;
using Bank.Application.Deposit;
using Bank.Application.PrintStatement;
using Bank.Application.Withdraw;
using Bank.Domain.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supabase;

namespace Bank.Tests;

public class UseCasesWithSupabaseTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private static readonly AccountId AccountId = new(42);

    private readonly IConfiguration _configuration = factory.Services.GetRequiredService<IConfiguration>();
    private readonly SystemClock _clock = new();

    private SupabaseOperations Operations => new(_configuration["Supabase:Url"]!, _configuration["Supabase:Key"]!);
    private DepositUseCase Deposit => new(Operations, _clock);
    private WithdrawUseCase Withdraw => new(Operations, _clock);
    private PrintStatementUseCase PrintStatement => new(Operations);
```

Dans `AtmScreenTests`, remplacez le `using Bank.Application.Services` par les namespaces des cas d'utilisation, et la création de l'écran :

```csharp
        var clock = new FixedClock(Now);
        var screen = new AtmScreen(
            Id,
            new DepositUseCase(_operations, clock),
            new WithdrawUseCase(_operations, clock),
            new PrintStatementUseCase(_operations),
            input,
            output);
```

Les 27 tests passent.

---

## Étape 2 : Des structures dédiées aux frontières

Aujourd'hui, le cas d'utilisation du relevé renvoie des lignes de relevé, un objet du domaine, jusqu'au contrôleur et à l'écran. On protège cette frontière, sans changer ce que voient les utilisateurs.

### Le Request Model et le Response Model

Ces structures ne servent qu'au relevé : elles vont dans son dossier.

Créez `src/Bank.Application/PrintStatement/PrintStatementModels.cs` :

```csharp
namespace Bank.Application.PrintStatement;

public record PrintStatementRequest(int AccountId);

public record PrintStatementLine(DateTime Date, decimal Amount, decimal Balance);

public record PrintStatementResponse(IReadOnlyList<PrintStatementLine> Lines, decimal Balance);
```

Ce sont de simples structures de données : elles ne contiennent que des types de base, et rien du domaine.

Remplacez `IForPrintingStatements.cs` :

```csharp
namespace Bank.Application.PrintStatement;

public interface IForPrintingStatements
{
    Task<PrintStatementResponse> PrintStatementAsync(PrintStatementRequest request);
}
```

Et `PrintStatementUseCase.cs` :

```csharp
using Bank.Application.Ports;
using Bank.Domain.Models;

namespace Bank.Application.PrintStatement;

public class PrintStatementUseCase(IOperations operations) : IForPrintingStatements
{
    public async Task<PrintStatementResponse> PrintStatementAsync(PrintStatementRequest request)
    {
        var account = await operations.AccountOfAsync(new AccountId(request.AccountId));

        var lines = account.Statement()
            .Select(line => new PrintStatementLine(line.Date, line.Amount.Value, line.Balance.Value))
            .ToList();

        return new PrintStatementResponse(lines, account.Balance.Value);
    }
}
```

Le solde vient du compte : c'est lui qui le calcule, et le Response Model le transporte.

### Les presenters

`src/Bank.Adapters.Rest/Presenters/StatementJsonPresenter.cs`

```csharp
using Bank.Adapters.Rest.Models;
using Bank.Application.PrintStatement;

namespace Bank.Adapters.Rest.Presenters;

public static class StatementJsonPresenter
{
    public static IReadOnlyList<StatementLineResponse> Present(PrintStatementResponse response) =>
        response.Lines
            .Select(line => new StatementLineResponse(line.Date, line.Amount, line.Balance))
            .ToList();
}
```

Dans `AccountsController`, ajoutez `using Bank.Adapters.Rest.Presenters;` et remplacez `GetStatement` :

```csharp
    [HttpGet("operations")]
    public async Task<IReadOnlyList<StatementLineResponse>> GetStatement(int accountId)
    {
        var response = await printingStatements.PrintStatementAsync(new PrintStatementRequest(accountId));

        return StatementJsonPresenter.Present(response);
    }
```

`src/Bank.Adapters.Cli/StatementAtmPresenter.cs`

```csharp
using Bank.Application.PrintStatement;

namespace Bank.Adapters.Cli;

public static class StatementAtmPresenter
{
    public static string Present(PrintStatementResponse response)
    {
        var lines = response.Lines
            .Select(line => $"{line.Date:yyyy-MM-dd} | {line.Amount} | {line.Balance}")
            .Append($"Balance: {response.Balance}");

        return string.Join(Environment.NewLine, lines);
    }
}
```

Dans `AtmScreen`, remplacez `PrintStatementAsync` :

```csharp
    private async Task PrintStatementAsync()
    {
        var response = await printingStatements.PrintStatementAsync(new PrintStatementRequest(accountId.Value));

        await output.WriteLineAsync(StatementAtmPresenter.Present(response));
    }
```

Le test de l'écran et le test de bout en bout ne changent pas : c'est la preuve que l'affichage et le JSON sont identiques.

### Adapter les tests du relevé

Ces tests vérifient maintenant un Response Model. Dans `PrintStatementTests`, remplacez la fin du test :

```csharp
        var response = await _printingStatements.PrintStatementAsync(new PrintStatementRequest(Id.Value));

        var line = Assert.Single(response.Lines);
        Assert.Equal(new PrintStatementLine(Now, 100, 100), line);
        Assert.Equal(100, response.Balance);
```

Dans `UseCasesWithSupabaseTests`, ajoutez cette méthode :

```csharp
    private Task<PrintStatementResponse> StatementAsync() =>
        PrintStatement.PrintStatementAsync(new PrintStatementRequest(AccountId.Value));
```

Remplacez chaque appel au relevé par `StatementAsync()`, et lisez `.Lines` et `.Balance` sur son résultat. Les montants sont maintenant des `decimal` : `Assert.Equal(100, line.Balance)`.

Les 27 tests passent.

`Deposit` et `Withdraw` utilisent encore `AccountId`, `Amount` et les exceptions du domaine. Faudrait-il leur appliquer la même chose ? C'est la question du coût : chaque structure est du code à écrire et à maintenir, pour un gain faible quand la donnée est aussi simple.

### Le nouveau solde après un retrait

Le relevé protégeait une frontière. Le retrait illustre l'autre apport de la Clean Architecture : les règles propres à l'application. Après un retrait, l'écran du DAB affiche le nouveau solde. Cette règle n'existe pas pour la banque, elle n'existe que parce que l'application existe : c'est le cas d'utilisation qui la porte, et le compte ne change pas.

#### Le cas d'utilisation renvoie le nouveau solde

**Rouge**

Ajoutez ce test à `WithdrawTests`. Il ne compile pas : `WithdrawAsync` ne renvoie rien.

```csharp
    [Fact]
    public async Task Withdraw_ReturnsTheNewBalance()
    {
        await _depositing.DepositAsync(Id, new Amount(100));

        var response = await _withdrawing.WithdrawAsync(Id, new Amount(30));

        Assert.Equal(70, response.Balance);
    }
```

**Vert**

Créez `src/Bank.Application/Withdraw/WithdrawResponse.cs` :

```csharp
namespace Bank.Application.Withdraw;

public record WithdrawResponse(decimal Balance);
```

Remplacez `IForWithdrawing.cs` :

```csharp
using Bank.Domain.Models;

namespace Bank.Application.Withdraw;

public interface IForWithdrawing
{
    Task<WithdrawResponse> WithdrawAsync(AccountId accountId, Amount amount);
}
```

Et `WithdrawUseCase.cs` :

```csharp
using Bank.Application.Ports;
using Bank.Domain.Models;

namespace Bank.Application.Withdraw;

public class WithdrawUseCase(IOperations operations, IClock clock) : IForWithdrawing
{
    public async Task<WithdrawResponse> WithdrawAsync(AccountId accountId, Amount amount)
    {
        var account = await operations.AccountOfAsync(accountId);
        account.Withdraw(amount, clock.Now());
        await operations.SaveAsync(account);

        return new WithdrawResponse(account.Balance.Value);
    }
}
```

Les 28 tests passent. `AccountsController` ne change pas : il ignore cette réponse, et l'API renvoie toujours `201`.

#### L'écran du DAB affiche le nouveau solde

**Rouge**

Ajoutez ce test à `AtmScreenTests` :

```csharp
    [Fact]
    public async Task Withdrawal_DisplaysTheNewBalance()
    {
        var output = await RunAsync("deposit 100", "withdraw 30");

        Assert.Contains("Balance: 70", output);
    }
```

Le test échoue : l'écran n'affiche rien après un retrait.

**Vert**

Dans `AtmScreen`, remplacez `WithdrawAsync` :

```csharp
    private async Task WithdrawAsync(Amount amount)
    {
        try
        {
            var response = await withdrawing.WithdrawAsync(accountId, amount);

            await output.WriteLineAsync($"Balance: {response.Balance}");
        }
        catch (WithdrawalRefusedException exception)
        {
            await output.WriteLineAsync(exception.Message);
        }
    }
```

Les 29 tests passent.

---

## Les tests d'architecture

Créez `tests/Bank.Architecture.Tests/Bank.Architecture.Tests.csproj` :

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\src\Bank.Adapters.Cli\Bank.Adapters.Cli.csproj" />
    <ProjectReference Include="..\..\src\Bank.Adapters.Rest\Bank.Adapters.Rest.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="TngTech.ArchUnitNET.xUnitV3" Version="0.13.5" />
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
dotnet sln add tests/Bank.Architecture.Tests
```

Créez `tests/Bank.Architecture.Tests/ArchitectureTests.cs` :

```csharp
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Bank.Adapters.Cli;
using Bank.Adapters.Rest.Controllers;
using Bank.Adapters.Supabase;
using Bank.Application.Ports;
using Bank.Domain.Models;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using BankArchitecture = ArchUnitNET.Domain.Architecture;

namespace Bank.Architecture.Tests;

public class ArchitectureTests
{
    private static readonly BankArchitecture Code = new ArchLoader()
        .LoadAssemblies(
            typeof(Account).Assembly,
            typeof(IOperations).Assembly,
            typeof(SupabaseOperations).Assembly,
            typeof(AccountsController).Assembly,
            typeof(AtmScreen).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> Domain =
        Types().That().ResideInNamespaceMatching(@"Bank\.Domain(\..*)?").As("le domaine");

    private static readonly IObjectProvider<IType> Application =
        Types().That().ResideInNamespaceMatching(@"Bank\.Application(\..*)?").As("l'application");

    private static readonly IObjectProvider<IType> Adapters =
        Types().That().ResideInNamespaceMatching(@"Bank\.Adapters(\..*)?").As("les adapters");

    private static readonly IObjectProvider<IType> SupabaseAdapter =
        Types().That().ResideInNamespaceMatching(@"Bank\.Adapters\.Supabase(\..*)?").As("l'adapter Supabase");

    private static readonly IObjectProvider<IType> IncomingAdapters =
        Types().That().ResideInNamespaceMatching(@"Bank\.Adapters\.(Rest|Cli)(\..*)?").As("les adapters entrants");

    [Fact]
    public void LeDomaineNeDependDAucunAutreModule() =>
        Types().That().Are(Domain)
            .Should().NotDependOnAny(Application).AndShould().NotDependOnAny(Adapters)
            .Check(Code);

    [Fact]
    public void LApplicationNeDependQueDuDomaine() =>
        Types().That().Are(Application)
            .Should().NotDependOnAny(Adapters)
            .Check(Code);

    [Fact]
    public void LesAdaptersEntrantsNeDependentPasDeLAdapterSupabase() =>
        Types().That().Are(IncomingAdapters)
            .Should().NotDependOnAny(SupabaseAdapter)
            .Check(Code);

    [Fact]
    public void LAdapterSupabaseNeDependPasDesAdaptersEntrants() =>
        Types().That().Are(SupabaseAdapter)
            .Should().NotDependOnAny(IncomingAdapters)
            .Check(Code);
}
```

Trois détails :

- `ResideInNamespace` ne regarde que le namespace exact. `Bank.Domain` ne contient aucun type, puisqu'ils sont tous dans `Bank.Domain.Models` : d'où `ResideInNamespaceMatching`
- Le namespace du projet contient `Architecture`, comme le type d'ArchUnitNET : d'où l'alias `BankArchitecture`
- `Program.cs` connaît les adapters, et ces règles l'acceptent : ses instructions de premier niveau ne sont dans aucun namespace `Bank`. C'est le seul endroit qui a le droit de tout assembler

Les 33 tests passent.

### Enfreindre une règle

En C#, les projets protègent déjà une partie de ces règles. `Bank.Domain` ne peut pas appeler Supabase : il n'en a pas la référence, et l'ajouter créerait une dépendance circulaire. Le test le plus utile porte donc sur ce que le compilateur laisse passer : `Bank.Adapters.Cli` référence l'adapter Supabase pour l'assembler dans `Program.cs`, donc `AtmScreen` pourrait l'appeler directement.

Ajoutez à `AtmScreen` :

```csharp
    private static readonly Type Shortcut = typeof(SupabaseOperations);
```

Avec `using Bank.Adapters.Supabase;`, le code compile, mais le test échoue :

```text
"Types that are les adapters entrants should not depend on any l'adapter Supabase" failed:
    Bank.Adapters.Cli.AtmScreen does depend on "Bank.Adapters.Supabase.SupabaseOperations"
```

Le message nomme la classe fautive et la dépendance interdite. Retirez cette ligne : les 33 tests passent.

