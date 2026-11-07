# Jour 4 : Solution ({{language}})

Ce guide reprend pas à pas le passage de l'hexagone à la Clean Architecture, puis les tests d'architecture. Il part du projet de `jour-4/start/`.

## Le projet de départ

<!-- lang:csharp -->
Ce projet a ses propres fichiers `appsettings.local.json`, qui ne sont pas versionnés : si vous ne les avez pas encore, suivez le README du projet pour créer ceux de `src/Bank.Adapters.Rest` et de `src/Bank.Adapters.Cli`.

Démarrez la base, puis lancez les tests depuis `jour-4/start/csharp` :

```bash
dotnet test
```

Les 27 tests passent. Toutes les commandes de ce guide se lancent depuis ce dossier.
<!-- /lang -->

<!-- lang:ts -->
Ce projet a son propre `.env`, qui n'est pas versionné : si vous ne l'avez pas encore, suivez le README du projet pour le créer à la racine de `jour-4/start/ts`.

Démarrez la base, puis installez les dépendances et lancez les tests depuis `jour-4/start/ts` :

```bash
npm install
npm test
```

Les 27 tests passent. Toutes les commandes de ce guide se lancent depuis ce dossier.
<!-- /lang -->

---

## Étape 1 : Séparer l'application du domaine

C'est un refactoring : aucun comportement ne change, et les tests existants servent de filet. C'est pourquoi cette étape ne suit pas le cycle Rouge, Vert : aucune règle nouvelle ne pilote le code.

### Créer le projet d'application

<!-- lang:csharp -->
```bash
dotnet new classlib -n Bank.Application -o src/Bank.Application
dotnet sln add src/Bank.Application
dotnet add src/Bank.Application reference src/Bank.Domain
```

Supprimez `Class1.cs`.
<!-- /lang -->

<!-- lang:ts -->
Créez `packages/application/package.json` :

```json
{
  "name": "@bank/application",
  "version": "1.0.0",
  "private": true,
  "type": "module",
  "exports": {
    ".": "./src/index.ts"
  },
  "dependencies": {
    "@bank/domain": "*"
  }
}
```
<!-- /lang -->

### Déplacer les ports et le service

Les ports entrants décrivent ce que propose l'application, et les ports sortants ce dont elle a besoin : ils appartiennent au cercle Use Cases. Le domaine ne garde que les entités et leurs règles.

<!-- lang:csharp -->
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
<!-- /lang -->

<!-- lang:ts -->
1. Déplacez les dossiers `ports` et `services` de `packages/domain/src` vers `packages/application/src`
2. Déplacez `accountService.test.ts`, `inMemoryOperations.ts` et `fixedClock.ts` de `packages/domain/tests` vers `packages/application/tests`
3. Dans les fichiers déplacés, remplacez les imports de `../models/...` et de `../src/models/...` par `@bank/domain`. Par exemple, `operations.ts` :

```typescript
import type { Account, AccountId } from "@bank/domain";

export interface Operations {
  accountOf(accountId: AccountId): Promise<Account>;

  save(account: Account): Promise<void>;
}
```

Le domaine n'expose plus que ses entités. Remplacez `packages/domain/src/index.ts` :

```typescript
export { Account } from "./models/account.ts";
export { AccountId } from "./models/accountId.ts";
export { Amount } from "./models/amount.ts";
export { InsufficientFundsError } from "./models/insufficientFundsError.ts";
export { NightWithdrawalLimitExceededError } from "./models/nightWithdrawalLimitExceededError.ts";
export { WithdrawalLimitExceededError } from "./models/withdrawalLimitExceededError.ts";
export { WithdrawalRefusedError } from "./models/withdrawalRefusedError.ts";
export type { Operation } from "./models/operation.ts";
export type { StatementLine } from "./models/statementLine.ts";
```

Et créez `packages/application/src/index.ts` :

```typescript
export type { Clock } from "./ports/clock.ts";
export type { ForDepositing } from "./ports/forDepositing.ts";
export type { ForPrintingStatements } from "./ports/forPrintingStatements.ts";
export type { ForWithdrawing } from "./ports/forWithdrawing.ts";
export type { Operations } from "./ports/operations.ts";
export { AccountService } from "./services/accountService.ts";
```

Faites pointer les adapters vers l'application, dans leur `package.json` :

- `adapter-supabase`, `adapter-rest` et `adapter-cli` : ajoutez `"@bank/application": "*"`
- `adapter-time` : remplacez `@bank/domain` par `@bank/application`

Puis, dans leurs imports, les ports et `AccountService` viennent maintenant de `@bank/application`, et les entités de `@bank/domain`. Par exemple, dans `supabaseOperations.ts` :

```typescript
import type { Operations } from "@bank/application";
import type { AccountId } from "@bank/domain";
```

Dans `packages/adapter-cli/tests/atmScreen.test.ts`, remplacez `../../domain/tests/` par `../../application/tests/`.

Lancez `npm install` : les 27 tests passent, et `npm run typecheck` aussi.
<!-- /lang -->

### Un dossier par cas d'utilisation

Le service disparaît. Chaque cas d'utilisation reçoit son propre dossier, avec tout ce qui lui est propre : on regroupe le code par ce qu'il fait, et non par ce qu'il est. En ouvrant le projet d'application, on lit ce qu'elle fait, et travailler sur le relevé ne demande d'ouvrir qu'un seul dossier.

<!-- lang:csharp -->
```text
Bank.Application/
├── Deposit/          # IForDepositing, DepositUseCase
├── PrintStatement/   # IForPrintingStatements, PrintStatementUseCase
├── Withdraw/         # IForWithdrawing, WithdrawUseCase
└── Ports/            # IOperations, IClock : partagés par plusieurs cas d'utilisation
```
<!-- /lang -->

<!-- lang:ts -->
```text
packages/application/src/
├── deposit/          # ForDepositing, DepositUseCase
├── printStatement/   # ForPrintingStatements, PrintStatementUseCase
├── withdraw/         # ForWithdrawing, WithdrawUseCase
└── ports/            # Operations, Clock : partagés par plusieurs cas d'utilisation
```
<!-- /lang -->

<!-- lang:csharp -->
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
<!-- /lang -->

<!-- lang:ts -->
Déplacez chaque port entrant dans le dossier de son cas d'utilisation.

`packages/application/src/deposit/depositUseCase.ts`

```typescript
import type { AccountId, Amount } from "@bank/domain";
import type { Clock } from "../ports/clock.ts";
import type { Operations } from "../ports/operations.ts";
import type { ForDepositing } from "./forDepositing.ts";

export class DepositUseCase implements ForDepositing {
  private readonly operations: Operations;
  private readonly clock: Clock;

  constructor(operations: Operations, clock: Clock) {
    this.operations = operations;
    this.clock = clock;
  }

  async deposit(accountId: AccountId, amount: Amount): Promise<void> {
    const account = await this.operations.accountOf(accountId);
    account.deposit(amount, this.clock.now());
    await this.operations.save(account);
  }
}
```

`packages/application/src/withdraw/withdrawUseCase.ts`

```typescript
import type { AccountId, Amount } from "@bank/domain";
import type { Clock } from "../ports/clock.ts";
import type { Operations } from "../ports/operations.ts";
import type { ForWithdrawing } from "./forWithdrawing.ts";

export class WithdrawUseCase implements ForWithdrawing {
  private readonly operations: Operations;
  private readonly clock: Clock;

  constructor(operations: Operations, clock: Clock) {
    this.operations = operations;
    this.clock = clock;
  }

  async withdraw(accountId: AccountId, amount: Amount): Promise<void> {
    const account = await this.operations.accountOf(accountId);
    account.withdraw(amount, this.clock.now());
    await this.operations.save(account);
  }
}
```

`packages/application/src/printStatement/printStatementUseCase.ts`

```typescript
import type { AccountId, StatementLine } from "@bank/domain";
import type { Operations } from "../ports/operations.ts";
import type { ForPrintingStatements } from "./forPrintingStatements.ts";

export class PrintStatementUseCase implements ForPrintingStatements {
  private readonly operations: Operations;

  constructor(operations: Operations) {
    this.operations = operations;
  }

  async printStatement(accountId: AccountId): Promise<StatementLine[]> {
    const account = await this.operations.accountOf(accountId);
    return account.statement();
  }
}
```

Supprimez le dossier `src/services`, et remplacez `packages/application/src/index.ts` :

```typescript
export { DepositUseCase } from "./deposit/depositUseCase.ts";
export type { ForDepositing } from "./deposit/forDepositing.ts";
export type { Clock } from "./ports/clock.ts";
export type { Operations } from "./ports/operations.ts";
export type { ForPrintingStatements } from "./printStatement/forPrintingStatements.ts";
export { PrintStatementUseCase } from "./printStatement/printStatementUseCase.ts";
export type { ForWithdrawing } from "./withdraw/forWithdrawing.ts";
export { WithdrawUseCase } from "./withdraw/withdrawUseCase.ts";
```

Remplacez `packages/adapter-rest/src/app.ts` :

```typescript
import express from "express";
import type { Express } from "express";
import { SupabaseOperations } from "@bank/adapter-supabase";
import { SystemClock } from "@bank/adapter-time";
import { DepositUseCase, PrintStatementUseCase, WithdrawUseCase } from "@bank/application";
import { accountsController } from "./accountsController.ts";

export function createApp(): Express {
  const operations = new SupabaseOperations(process.env.SUPABASE_URL!, process.env.SUPABASE_KEY!);
  const clock = new SystemClock();
  const app = express();

  app.use(express.json());
  app.use(
    accountsController(
      new DepositUseCase(operations, clock),
      new WithdrawUseCase(operations, clock),
      new PrintStatementUseCase(operations)
    )
  );

  return app;
}
```

Et `packages/adapter-cli/src/main.ts` :

```typescript
import { createInterface } from "node:readline";
import { SupabaseOperations } from "@bank/adapter-supabase";
import { SystemClock } from "@bank/adapter-time";
import { DepositUseCase, PrintStatementUseCase, WithdrawUseCase } from "@bank/application";
import { AccountId } from "@bank/domain";
import { AtmScreen } from "./atmScreen.ts";

const operations = new SupabaseOperations(process.env.SUPABASE_URL!, process.env.SUPABASE_KEY!);
const clock = new SystemClock();

const screen = new AtmScreen(
  new AccountId(Number(process.argv[2])),
  new DepositUseCase(operations, clock),
  new WithdrawUseCase(operations, clock),
  new PrintStatementUseCase(operations),
  createInterface({ input: process.stdin }),
  console.log
);

console.log("Commands: deposit <amount>, withdraw <amount>, statement, quit");
await screen.run();
```

`atmScreen.ts` et `accountsController.ts` ne changent pas : ils ne connaissent que les ports. C'est ce qui rend ce découpage aussi peu coûteux.

Il reste les tests, qui suivent le même découpage. Supprimez `packages/application/tests/accountService.test.ts`, et créez :

`packages/application/tests/deposit/deposit.test.ts`

```typescript
import { beforeEach, expect, test } from "vitest";
import { AccountId, Amount } from "@bank/domain";
import { DepositUseCase } from "../../src/deposit/depositUseCase.ts";
import { FixedClock } from "../fixedClock.ts";
import { InMemoryOperations } from "../inMemoryOperations.ts";
import type { ForDepositing } from "../../src/deposit/forDepositing.ts";

const id = new AccountId(1);
const now = new Date("2021-01-10T00:00:00Z");

let operations: InMemoryOperations;
let depositing: ForDepositing;

beforeEach(() => {
  operations = new InMemoryOperations();
  depositing = new DepositUseCase(operations, new FixedClock(now));
});

test("a deposit saves the operation at the current date", async () => {
  await depositing.deposit(id, new Amount(100));

  const account = await operations.accountOf(id);
  expect(account.history).toEqual([{ amount: new Amount(100), date: now }]);
});
```

`packages/application/tests/withdraw/withdraw.test.ts`

```typescript
import { beforeEach, expect, test } from "vitest";
import { AccountId, Amount } from "@bank/domain";
import { DepositUseCase } from "../../src/deposit/depositUseCase.ts";
import { WithdrawUseCase } from "../../src/withdraw/withdrawUseCase.ts";
import { FixedClock } from "../fixedClock.ts";
import { InMemoryOperations } from "../inMemoryOperations.ts";
import type { ForDepositing } from "../../src/deposit/forDepositing.ts";
import type { ForWithdrawing } from "../../src/withdraw/forWithdrawing.ts";

const id = new AccountId(1);
const now = new Date("2021-01-10T00:00:00Z");

let operations: InMemoryOperations;
let depositing: ForDepositing;
let withdrawing: ForWithdrawing;

beforeEach(() => {
  const clock = new FixedClock(now);
  operations = new InMemoryOperations();
  depositing = new DepositUseCase(operations, clock);
  withdrawing = new WithdrawUseCase(operations, clock);
});

test("a withdrawal saves the operation at the current date", async () => {
  await depositing.deposit(id, new Amount(100));

  await withdrawing.withdraw(id, new Amount(30));

  const account = await operations.accountOf(id);
  expect(account.history.at(-1)).toEqual({ amount: new Amount(-30), date: now });
});
```

`packages/application/tests/printStatement/printStatement.test.ts`

```typescript
import { beforeEach, expect, test } from "vitest";
import { AccountId, Amount } from "@bank/domain";
import { DepositUseCase } from "../../src/deposit/depositUseCase.ts";
import { PrintStatementUseCase } from "../../src/printStatement/printStatementUseCase.ts";
import { FixedClock } from "../fixedClock.ts";
import { InMemoryOperations } from "../inMemoryOperations.ts";
import type { ForDepositing } from "../../src/deposit/forDepositing.ts";
import type { ForPrintingStatements } from "../../src/printStatement/forPrintingStatements.ts";

const id = new AccountId(1);
const now = new Date("2021-01-10T00:00:00Z");

let depositing: ForDepositing;
let printingStatements: ForPrintingStatements;

beforeEach(() => {
  const operations = new InMemoryOperations();
  depositing = new DepositUseCase(operations, new FixedClock(now));
  printingStatements = new PrintStatementUseCase(operations);
});

test("printing a statement returns the statement of the account", async () => {
  await depositing.deposit(id, new Amount(100));

  const statement = await printingStatements.printStatement(id);

  expect(statement).toEqual([{ date: now, amount: new Amount(100), balance: new Amount(100) }]);
});
```

Renommez `packages/adapter-rest/tests/accountService.test.ts` en `useCasesWithSupabase.test.ts`, et importez les cas d'utilisation. `createService` disparaît au profit de ces fonctions, et chaque `service.` devient l'appel concerné, par exemple `createDeposit().deposit(...)` :

```typescript
function createOperations(): SupabaseOperations {
  return new SupabaseOperations(process.env.SUPABASE_URL!, process.env.SUPABASE_KEY!);
}

function createDeposit(): DepositUseCase {
  return new DepositUseCase(createOperations(), new SystemClock());
}

function createWithdraw(): WithdrawUseCase {
  return new WithdrawUseCase(createOperations(), new SystemClock());
}

function createPrintStatement(): PrintStatementUseCase {
  return new PrintStatementUseCase(createOperations());
}
```

Dans `atmScreen.test.ts`, importez aussi les cas d'utilisation, et remplacez `run` :

```typescript
async function run(...commands: string[]): Promise<string[]> {
  const output: string[] = [];
  const clock = new FixedClock(now);
  const screen = new AtmScreen(
    id,
    new DepositUseCase(operations, clock),
    new WithdrawUseCase(operations, clock),
    new PrintStatementUseCase(operations),
    commands,
    (line) => output.push(line)
  );

  await screen.run();

  return output;
}
```

Les 27 tests passent.
<!-- /lang -->

---

## Étape 2 : Des structures dédiées aux frontières

Aujourd'hui, le cas d'utilisation du relevé renvoie des lignes de relevé, un objet du domaine, jusqu'au contrôleur et à l'écran. On protège cette frontière, sans changer ce que voient les utilisateurs.

### Le Request Model et le Response Model

Ces structures ne servent qu'au relevé : elles vont dans son dossier.

<!-- lang:csharp -->
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
<!-- /lang -->

<!-- lang:ts -->
Créez `packages/application/src/printStatement/printStatementModels.ts` :

```typescript
export interface PrintStatementRequest {
  accountId: number;
}

export interface PrintStatementLine {
  date: Date;
  amount: number;
  balance: number;
}

export interface PrintStatementResponse {
  lines: PrintStatementLine[];
  balance: number;
}
```

Ce sont de simples structures de données : elles ne contiennent que des types de base, et rien du domaine.

Remplacez `forPrintingStatements.ts` :

```typescript
import type { PrintStatementRequest, PrintStatementResponse } from "./printStatementModels.ts";

export interface ForPrintingStatements {
  printStatement(request: PrintStatementRequest): Promise<PrintStatementResponse>;
}
```

Et `printStatementUseCase.ts` :

```typescript
import { AccountId } from "@bank/domain";
import type { Operations } from "../ports/operations.ts";
import type { ForPrintingStatements } from "./forPrintingStatements.ts";
import type { PrintStatementRequest, PrintStatementResponse } from "./printStatementModels.ts";

export class PrintStatementUseCase implements ForPrintingStatements {
  private readonly operations: Operations;

  constructor(operations: Operations) {
    this.operations = operations;
  }

  async printStatement(request: PrintStatementRequest): Promise<PrintStatementResponse> {
    const account = await this.operations.accountOf(new AccountId(request.accountId));

    const lines = account.statement().map(({ date, amount, balance }) => ({
      date,
      amount: amount.value,
      balance: balance.value
    }));

    return { lines, balance: account.balance.value };
  }
}
```

Le solde vient du compte : c'est lui qui le calcule, et le Response Model le transporte.

Exposez ces structures dans `packages/application/src/index.ts` :

```typescript
export type {
  PrintStatementLine,
  PrintStatementRequest,
  PrintStatementResponse
} from "./printStatement/printStatementModels.ts";
```
<!-- /lang -->

### Les presenters

<!-- lang:csharp -->
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
<!-- /lang -->

<!-- lang:ts -->
`packages/adapter-rest/src/presenters/statementJsonPresenter.ts`

```typescript
import type { PrintStatementResponse } from "@bank/application";
import type { StatementLineResponse } from "../models.ts";

export function presentStatementAsJson(response: PrintStatementResponse): StatementLineResponse[] {
  return response.lines.map(({ date, amount, balance }) => ({ date, amount, balance }));
}
```

Dans `accountsController.ts`, importez `presentStatementAsJson` et remplacez la route du relevé :

```typescript
  router.get("/accounts/:accountId/operations", async (request: Request, response: Response<StatementLineResponse[]>) => {
    const statement = await printingStatements.printStatement({ accountId: Number(request.params.accountId) });

    response.json(presentStatementAsJson(statement));
  });
```

`packages/adapter-cli/src/statementAtmPresenter.ts`

```typescript
import type { PrintStatementResponse } from "@bank/application";

export function presentStatementForAtm(response: PrintStatementResponse): string[] {
  const lines = response.lines.map(
    ({ date, amount, balance }) => `${date.toISOString().slice(0, 10)} | ${amount} | ${balance}`
  );

  return [...lines, `Balance: ${response.balance}`];
}
```

Dans `atmScreen.ts`, importez `presentStatementForAtm` et remplacez `printStatement` :

```typescript
  private async printStatement(): Promise<void> {
    const response = await this.printingStatements.printStatement({ accountId: this.accountId.value });

    for (const line of presentStatementForAtm(response)) {
      this.output(line);
    }
  }
```

Le test de l'écran et le test de bout en bout ne changent pas : c'est la preuve que l'affichage et le JSON sont identiques.
<!-- /lang -->

### Adapter les tests du relevé

<!-- lang:csharp -->
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
<!-- /lang -->

<!-- lang:ts -->
Ces tests vérifient maintenant un Response Model. Dans `printStatement.test.ts`, remplacez la fin du test :

```typescript
  const response = await printingStatements.printStatement({ accountId: id.value });

  expect(response).toEqual({ lines: [{ date: now, amount: 100, balance: 100 }], balance: 100 });
```

Dans `useCasesWithSupabase.test.ts`, importez le type `PrintStatementResponse`, et ajoutez cette fonction :

```typescript
function statement(): Promise<PrintStatementResponse> {
  return createPrintStatement().printStatement({ accountId: accountId.value });
}
```

Remplacez chaque appel au relevé par `statement()`, et lisez `lines` et `balance` sur son résultat. Les montants sont maintenant de simples nombres : `expect(response.balance).toBe(100)`.

Les 27 tests passent.

`deposit` et `withdraw` utilisent encore `AccountId`, `Amount` et les erreurs du domaine. Faudrait-il leur appliquer la même chose ? C'est la question du coût : chaque structure est du code à écrire et à maintenir, pour un gain faible quand la donnée est aussi simple.
<!-- /lang -->

### Le nouveau solde après un retrait

Le relevé protégeait une frontière. Le retrait illustre l'autre apport de la Clean Architecture : les règles propres à l'application. Après un retrait, l'écran du DAB affiche le nouveau solde. Cette règle n'existe pas pour la banque, elle n'existe que parce que l'application existe : c'est le cas d'utilisation qui la porte, et le compte ne change pas.

#### Le cas d'utilisation renvoie le nouveau solde

**Rouge**

<!-- lang:csharp -->
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
<!-- /lang -->

<!-- lang:ts -->
Ajoutez ce test à `withdraw.test.ts`. Il échoue : `withdraw` ne renvoie rien.

```typescript
test("a withdrawal returns the new balance", async () => {
  await depositing.deposit(id, new Amount(100));

  const response = await withdrawing.withdraw(id, new Amount(30));

  expect(response.balance).toBe(70);
});
```
<!-- /lang -->

**Vert**

<!-- lang:csharp -->
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
<!-- /lang -->

<!-- lang:ts -->
Créez `packages/application/src/withdraw/withdrawResponse.ts` :

```typescript
export interface WithdrawResponse {
  balance: number;
}
```

Remplacez `forWithdrawing.ts` :

```typescript
import type { AccountId, Amount } from "@bank/domain";
import type { WithdrawResponse } from "./withdrawResponse.ts";

export interface ForWithdrawing {
  withdraw(accountId: AccountId, amount: Amount): Promise<WithdrawResponse>;
}
```

Dans `withdrawUseCase.ts`, importez le type `WithdrawResponse` et remplacez `withdraw` :

```typescript
  async withdraw(accountId: AccountId, amount: Amount): Promise<WithdrawResponse> {
    const account = await this.operations.accountOf(accountId);
    account.withdraw(amount, this.clock.now());
    await this.operations.save(account);

    return { balance: account.balance.value };
  }
```

Exposez ce type dans `packages/application/src/index.ts` :

```typescript
export type { WithdrawResponse } from "./withdraw/withdrawResponse.ts";
```

Les 28 tests passent. `accountsController.ts` ne change pas : il ignore cette réponse, et l'API renvoie toujours `201`.
<!-- /lang -->

#### L'écran du DAB affiche le nouveau solde

**Rouge**

<!-- lang:csharp -->
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
<!-- /lang -->

<!-- lang:ts -->
Ajoutez ce test à `atmScreen.test.ts` :

```typescript
test("a withdrawal displays the new balance", async () => {
  const output = await run("deposit 100", "withdraw 30");

  expect(output).toEqual(["Balance: 70"]);
});
```

Le test échoue : l'écran n'affiche rien après un retrait.
<!-- /lang -->

**Vert**

<!-- lang:csharp -->
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
<!-- /lang -->

<!-- lang:ts -->
Dans `atmScreen.ts`, remplacez `withdraw` :

```typescript
  private async withdraw(amount: Amount): Promise<void> {
    try {
      const response = await this.withdrawing.withdraw(this.accountId, amount);

      this.output(`Balance: ${response.balance}`);
    } catch (error) {
      if (error instanceof WithdrawalRefusedError) {
        this.output(error.message);
      } else {
        throw error;
      }
    }
  }
```

Les 29 tests passent.
<!-- /lang -->

---

## Les tests d'architecture

<!-- lang:csharp -->
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
<!-- /lang -->

<!-- lang:ts -->
On utilise dependency-cruiser, qui lit les imports de tous les paquets et applique des règles. Il ne prend pas encore en charge TypeScript 7 : installez-le avec TypeScript 6.

```bash
npm install --save-dev dependency-cruiser typescript@^6
```

Ajoutez ce script au `package.json` de la racine :

```json
    "arch": "depcruise packages --output-type err",
```

Créez `.dependency-cruiser.cjs` à la racine :

```javascript
module.exports = {
  forbidden: [
    {
      name: "le-domaine-ne-depend-d-aucun-autre-module",
      severity: "error",
      from: { path: "^packages/domain/src" },
      to: { path: "(application|adapter-)" }
    },
    {
      name: "l-application-ne-depend-que-du-domaine",
      severity: "error",
      from: { path: "^packages/application/src" },
      to: { path: "adapter-" }
    },
    {
      name: "les-adapters-entrants-ne-dependent-pas-de-l-adapter-supabase",
      severity: "error",
      from: { path: "^packages/adapter-(rest|cli)/src", pathNot: "/(app|main)[.]ts$" },
      to: { path: "adapter-supabase" }
    },
    {
      name: "l-adapter-supabase-ne-depend-pas-des-adapters-entrants",
      severity: "error",
      from: { path: "^packages/adapter-supabase/src" },
      to: { path: "adapter-(rest|cli)" }
    }
  ],
  options: {
    doNotFollow: { path: "node_modules" },
    tsConfig: { fileName: "tsconfig.json" },
    tsPreCompilationDeps: true
  }
};
```

Deux détails :

- Le typage protège déjà le domaine : il n'a accès à aucun paquet qu'il ne déclare pas. Ces règles vérifient aussi ce que le typage laisse passer
- `app.ts` et `main.ts` connaissent les adapters, et la troisième règle les exclut avec `pathNot` : c'est le seul endroit qui a le droit de tout assembler

Lancez `npm run arch` : aucune règle n'est enfreinte. Cette vérification est séparée de `npm test`, mais elle se lance aussi dans une intégration continue.
<!-- /lang -->

### Enfreindre une règle

<!-- lang:csharp -->
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
<!-- /lang -->

<!-- lang:ts -->
Le test le plus utile porte sur ce que le typage laisse passer : `adapter-cli` dépend de l'adapter Supabase pour l'assembler dans `main.ts`, donc `AtmScreen` pourrait l'appeler directement.

Ajoutez à `atmScreen.ts` :

```typescript
import { SupabaseOperations } from "@bank/adapter-supabase";

export const shortcut = SupabaseOperations;
```

`npm run typecheck` passe, mais `npm run arch` échoue :

```text
error les-adapters-entrants-ne-dependent-pas-de-l-adapter-supabase: packages/adapter-cli/src/atmScreen.ts → @bank/adapter-supabase
```

Le message nomme la règle, le fichier fautif et la dépendance interdite. Retirez ces lignes : `npm run arch` passe de nouveau.
<!-- /lang -->
