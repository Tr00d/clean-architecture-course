# Jour 2 : Solution ({{language}})

Ce guide reprend pas à pas la construction du domaine en TDD. Il part du projet vide de `jour-2/hexagon/`.

## Le projet de départ

Le domaine vit dans son propre module, qui ne connaît ni l'API, ni la base. C'est le compilateur qui le garantit : ce module ne référence rien.

<!-- lang:csharp -->
`jour-2/hexagon/csharp` contient `Bank.Domain`, vide, et son projet de tests. Lancez les tests depuis ce dossier :

```bash
dotnet test
```

Aucun test n'est trouvé pour l'instant : c'est normal, et la commande se termine sans erreur.
<!-- /lang -->

<!-- lang:ts -->
`jour-2/hexagon/ts` contient un espace de travail npm, avec un paquet `packages/domain` vide. Le fichier `.npmrc` active `install-strategy=linked` : un paquet ne voit que ses propres dépendances, et le domaine n'en déclare aucune. Sans ce réglage, npm rassemblerait toutes les dépendances à la racine, et le domaine pourrait importer une librairie sans l'avoir déclarée.

Installez les dépendances et lancez les tests depuis ce dossier :

```bash
npm install
npm test
```

Aucun test n'est trouvé pour l'instant : c'est normal, et la commande se termine sans erreur. Toutes les commandes de ce guide se lancent depuis `jour-2/hexagon/ts`, et les chemins partent de `packages/domain`.

Vitest exécute le code sans vérifier les types. Lancez `npm run typecheck` pour les vérifier : à l'étape Rouge, c'est lui qui signale ce qui n'existe pas encore.
<!-- /lang -->

---

## Les règles du compte

On avance un test à la fois. Ici, plus besoin de configuration, de données à nettoyer ou de compte réservé : un test crée un compte en mémoire, et c'est tout.

### Un dépôt augmente le solde

Le montant et l'identifiant du compte ne sont pas de simples nombres : ce sont des **value objects**. Ils sont définis par leur valeur, sans identité propre : deux montants de 100 sont le même montant, alors que deux comptes au même solde restent deux comptes. On ne confond plus un montant avec un identifiant, et le calcul sur les montants a sa place.

**Rouge**

<!-- lang:csharp -->
Créez les types dont le test a besoin, dans `src/Bank.Domain/Models` :

`AccountId.cs`

```csharp
namespace Bank.Domain.Models;

public readonly record struct AccountId(int Value);
```

`Amount.cs`

```csharp
namespace Bank.Domain.Models;

public readonly record struct Amount(decimal Value)
{
    public static readonly Amount Zero = new(0);

    public static Amount operator +(Amount left, Amount right) => new(left.Value + right.Value);
}
```

`Operation.cs`

```csharp
namespace Bank.Domain.Models;

public record Operation(Amount Amount, DateTime Date);
```

`Account.cs`, sans aucune logique pour l'instant :

```csharp
namespace Bank.Domain.Models;

public class Account(AccountId id, IReadOnlyList<Operation> operations)
{
    public AccountId Id => id;

    public IReadOnlyList<Operation> Operations => operations;

    public Amount Balance => Amount.Zero;

    public void Deposit(Amount amount, DateTime date)
    {
    }
}
```

Créez `tests/Bank.Domain.Tests/AccountTests.cs` :

```csharp
using Bank.Domain.Models;
using Xunit;

namespace Bank.Domain.Tests;

public class AccountTests
{
    private static readonly AccountId Id = new(1);
    private static readonly DateTime Date = new(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Deposit_IncreasesTheBalance()
    {
        var account = new Account(Id, []);

        account.Deposit(new Amount(100), Date);

        Assert.Equal(new Amount(100), account.Balance);
    }
}
```

Lancez le test : il échoue, le solde vaut 0.
<!-- /lang -->

<!-- lang:ts -->
Créez les types dont le test a besoin, dans `src/models` :

`accountId.ts`

```typescript
export class AccountId {
  readonly value: number;

  constructor(value: number) {
    this.value = value;
  }
}
```

`amount.ts`

```typescript
export class Amount {
  static readonly zero = new Amount(0);

  readonly value: number;

  constructor(value: number) {
    this.value = value;
  }

  plus(other: Amount): Amount {
    return new Amount(this.value + other.value);
  }
}
```

`operation.ts`

```typescript
import type { Amount } from "./amount.ts";

export interface Operation {
  amount: Amount;
  date: Date;
}
```

`account.ts`, sans aucune logique pour l'instant :

```typescript
import { Amount } from "./amount.ts";
import type { AccountId } from "./accountId.ts";
import type { Operation } from "./operation.ts";

export class Account {
  readonly id: AccountId;
  readonly operations: Operation[];

  constructor(id: AccountId, operations: readonly Operation[]) {
    this.id = id;
    this.operations = [...operations];
  }

  get balance(): Amount {
    return Amount.zero;
  }

  deposit(amount: Amount, date: Date): void {
  }
}
```

Créez `tests/account.test.ts` :

```typescript
import { expect, test } from "vitest";
import { Account } from "../src/models/account.ts";
import { AccountId } from "../src/models/accountId.ts";
import { Amount } from "../src/models/amount.ts";

const id = new AccountId(1);
const date = new Date("2021-01-10T00:00:00Z");

test("a deposit increases the balance", () => {
  const account = new Account(id, []);

  account.deposit(new Amount(100), date);

  expect(account.balance).toEqual(new Amount(100));
});
```

Lancez le test : il échoue, le solde vaut 0.
<!-- /lang -->

**Vert**

<!-- lang:csharp -->
Remplacez le contenu de `Account` :

```csharp
namespace Bank.Domain.Models;

public class Account(AccountId id, IReadOnlyList<Operation> operations)
{
    private readonly List<Operation> _operations = [.. operations];

    public AccountId Id => id;

    public IReadOnlyList<Operation> Operations => _operations;

    public Amount Balance => _operations.Aggregate(Amount.Zero, (balance, operation) => balance + operation.Amount);

    public void Deposit(Amount amount, DateTime date)
    {
        _operations.Add(new Operation(amount, date));
    }
}
```

Le test passe.
<!-- /lang -->

<!-- lang:ts -->
Remplacez le contenu de `account.ts` :

```typescript
import { Amount } from "./amount.ts";
import type { AccountId } from "./accountId.ts";
import type { Operation } from "./operation.ts";

export class Account {
  readonly id: AccountId;
  readonly operations: Operation[];

  constructor(id: AccountId, operations: readonly Operation[]) {
    this.id = id;
    this.operations = [...operations];
  }

  get balance(): Amount {
    return this.operations.reduce((balance, operation) => balance.plus(operation.amount), Amount.zero);
  }

  deposit(amount: Amount, date: Date): void {
    this.operations.push({ amount, date });
  }
}
```

Le test passe.
<!-- /lang -->

### Un retrait diminue le solde

**Rouge**

<!-- lang:csharp -->
Ajoutez ce test à `AccountTests`. Il ne compile pas : `Withdraw` n'existe pas encore.

```csharp
    [Fact]
    public void Withdrawal_DecreasesTheBalance()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(100), Date);

        account.Withdraw(new Amount(30), Date);

        Assert.Equal(new Amount(70), account.Balance);
    }
```
<!-- /lang -->

<!-- lang:ts -->
Ajoutez ce test à `account.test.ts`. Il échoue : `withdraw` n'existe pas encore.

```typescript
test("a withdrawal decreases the balance", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(100), date);

  account.withdraw(new Amount(30), date);

  expect(account.balance).toEqual(new Amount(70));
});
```
<!-- /lang -->

**Vert**

<!-- lang:csharp -->
Un retrait est une opération négative. Ajoutez à `Amount` :

```csharp
    public static Amount operator -(Amount amount) => new(-amount.Value);
```

Et à `Account` :

```csharp
    public void Withdraw(Amount amount, DateTime date)
    {
        _operations.Add(new Operation(-amount, date));
    }
```

Les 2 tests passent.
<!-- /lang -->

<!-- lang:ts -->
Un retrait est une opération négative. Ajoutez à `Amount` :

```typescript
  negated(): Amount {
    return new Amount(-this.value);
  }
```

Et à `Account` :

```typescript
  withdraw(amount: Amount, date: Date): void {
    this.operations.push({ amount: amount.negated(), date });
  }
```

Les 2 tests passent.
<!-- /lang -->

### Un retrait supérieur au solde est refusé

**Rouge**

<!-- lang:csharp -->
Pour que le test compile, créez `src/Bank.Domain/Models/InsufficientFundsException.cs` :

```csharp
namespace Bank.Domain.Models;

public class InsufficientFundsException(Amount balance)
    : Exception($"Insufficient funds: the balance is {balance.Value}.");
```

Ajoutez ce test à `AccountTests` :

```csharp
    [Fact]
    public void Withdrawal_IsRefused_WhenAboveTheBalance()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(100), Date);

        Assert.Throws<InsufficientFundsException>(() => account.Withdraw(new Amount(100.01m), Date));

        Assert.Equal(new Amount(100), account.Balance);
    }
```

Le test échoue, car aucune exception n'est levée.
<!-- /lang -->

<!-- lang:ts -->
Pour que le test puisse s'exécuter, créez `src/models/insufficientFundsError.ts` :

```typescript
import type { Amount } from "./amount.ts";

export class InsufficientFundsError extends Error {
  constructor(balance: Amount) {
    super(`Insufficient funds: the balance is ${balance.value}.`);
  }
}
```

Ajoutez ce test à `account.test.ts`, et importez `InsufficientFundsError` :

```typescript
test("a withdrawal is refused when above the balance", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(100), date);

  expect(() => account.withdraw(new Amount(100.01), date)).toThrow(InsufficientFundsError);

  expect(account.balance).toEqual(new Amount(100));
});
```

Le test échoue, car aucune erreur n'est levée.
<!-- /lang -->

**Vert**

<!-- lang:csharp -->
Ajoutez à `Amount` :

```csharp
    public static bool operator >(Amount left, Amount right) => left.Value > right.Value;

    public static bool operator <(Amount left, Amount right) => left.Value < right.Value;
```

Et remplacez `Withdraw` dans `Account` :

```csharp
    public void Withdraw(Amount amount, DateTime date)
    {
        if (amount > Balance)
        {
            throw new InsufficientFundsException(Balance);
        }

        _operations.Add(new Operation(-amount, date));
    }
```

Les 3 tests passent.
<!-- /lang -->

<!-- lang:ts -->
Ajoutez à `Amount` :

```typescript
  isGreaterThan(other: Amount): boolean {
    return this.value > other.value;
  }

  isLessThan(other: Amount): boolean {
    return this.value < other.value;
  }
```

Dans `account.ts`, importez `InsufficientFundsError` et remplacez `withdraw` :

```typescript
  withdraw(amount: Amount, date: Date): void {
    if (amount.isGreaterThan(this.balance)) {
      throw new InsufficientFundsError(this.balance);
    }

    this.operations.push({ amount: amount.negated(), date });
  }
```

Les 3 tests passent.
<!-- /lang -->

### Un retrait égal au solde est accepté

Ce cas est déjà couvert par le cycle précédent : le test passera du premier coup, et il ne pilote aucun code. On sort donc du TDD le temps d'un test, pour fixer ce comportement à la limite.

<!-- lang:csharp -->
Ajoutez ce test à `AccountTests` :

```csharp
    [Fact]
    public void Withdrawal_IsAccepted_WhenEqualToTheBalance()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(100), Date);

        account.Withdraw(new Amount(100), Date);

        Assert.Equal(Amount.Zero, account.Balance);
    }
```

Un test qui n'a jamais échoué ne prouve rien. Vérifiez qu'il peut échouer en remplaçant temporairement `amount > Balance` par `!(Balance > amount)` : le test échoue. Remettez `amount > Balance`. Les 4 tests passent.
<!-- /lang -->

<!-- lang:ts -->
Ajoutez ce test à `account.test.ts` :

```typescript
test("a withdrawal is accepted when equal to the balance", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(100), date);

  account.withdraw(new Amount(100), date);

  expect(account.balance).toEqual(Amount.zero);
});
```

Un test qui n'a jamais échoué ne prouve rien. Vérifiez qu'il peut échouer en remplaçant temporairement `amount.isGreaterThan(this.balance)` par `!this.balance.isGreaterThan(amount)` : le test échoue. Remettez la condition. Les 4 tests passent.
<!-- /lang -->

### Le relevé

**Rouge**

C'est l'exemple du premier jour, avec ses dates. Hier, aucun test ne pouvait le reproduire : l'heure venait du service. Ici, la date fait partie des paramètres.

<!-- lang:csharp -->
Pour que le test compile, créez `src/Bank.Domain/Models/StatementLine.cs` :

```csharp
namespace Bank.Domain.Models;

public record StatementLine(DateTime Date, Amount Amount, Amount Balance);
```

Ajoutez à `Account` une méthode qui ne renvoie rien pour l'instant :

```csharp
    public IReadOnlyList<StatementLine> Statement()
    {
        return [];
    }
```

Ajoutez ce test à `AccountTests` :

```csharp
    [Fact]
    public void Statement_ListsOperationsNewestFirstWithRunningBalance()
    {
        var account = new Account(Id, []);
        account.Deposit(new Amount(1000), new DateTime(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        account.Deposit(new Amount(2000), new DateTime(2021, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        account.Withdraw(new Amount(500), new DateTime(2021, 1, 20, 0, 0, 0, DateTimeKind.Utc));

        var statement = account.Statement();

        StatementLine[] expected =
        [
            new(new DateTime(2021, 1, 20, 0, 0, 0, DateTimeKind.Utc), new Amount(-500), new Amount(2500)),
            new(new DateTime(2021, 1, 15, 0, 0, 0, DateTimeKind.Utc), new Amount(2000), new Amount(3000)),
            new(new DateTime(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc), new Amount(1000), new Amount(1000))
        ];
        Assert.Equal(expected, statement);
    }
```

Le test échoue : le relevé est vide.
<!-- /lang -->

<!-- lang:ts -->
Créez `src/models/statementLine.ts` :

```typescript
import type { Amount } from "./amount.ts";

export interface StatementLine {
  date: Date;
  amount: Amount;
  balance: Amount;
}
```

Importez ce type dans `account.ts`, et ajoutez à `Account` une méthode qui ne renvoie rien pour l'instant :

```typescript
  statement(): StatementLine[] {
    return [];
  }
```

Ajoutez ce test à `account.test.ts` :

```typescript
test("the statement lists the operations, newest first, with the running balance", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(1000), new Date("2021-01-10T00:00:00Z"));
  account.deposit(new Amount(2000), new Date("2021-01-15T00:00:00Z"));
  account.withdraw(new Amount(500), new Date("2021-01-20T00:00:00Z"));

  const statement = account.statement();

  expect(statement).toEqual([
    { date: new Date("2021-01-20T00:00:00Z"), amount: new Amount(-500), balance: new Amount(2500) },
    { date: new Date("2021-01-15T00:00:00Z"), amount: new Amount(2000), balance: new Amount(3000) },
    { date: new Date("2021-01-10T00:00:00Z"), amount: new Amount(1000), balance: new Amount(1000) }
  ]);
});
```

Le test échoue : le relevé est vide.
<!-- /lang -->

**Vert**

<!-- lang:csharp -->
Remplacez `Statement` dans `Account` :

```csharp
    public IReadOnlyList<StatementLine> Statement()
    {
        var lines = new List<StatementLine>();
        var balance = Amount.Zero;

        foreach (var operation in _operations)
        {
            balance += operation.Amount;
            lines.Add(new StatementLine(operation.Date, operation.Amount, balance));
        }

        lines.Reverse();
        return lines;
    }
```

Les 5 tests passent.
<!-- /lang -->

<!-- lang:ts -->
Remplacez `statement` dans `Account` :

```typescript
  statement(): StatementLine[] {
    const lines: StatementLine[] = [];
    let balance = Amount.zero;

    for (const operation of this.operations) {
      balance = balance.plus(operation.amount);
      lines.push({ date: operation.date, amount: operation.amount, balance });
    }

    return lines.reverse();
  }
```

Les 5 tests passent.
<!-- /lang -->

### Le relevé trie les opérations par date

**Rouge**

Hier, ce tri n'était pas vraiment testé. Il suffit ici de créer un compte dont les opérations ne sont pas dans l'ordre.

<!-- lang:csharp -->
Ajoutez ce test à `AccountTests` :

```csharp
    [Fact]
    public void Statement_SortsOperationsByDate_WhateverTheirOrder()
    {
        var account = new Account(
            Id,
            [
                new Operation(new Amount(-50), new DateTime(2021, 2, 2, 0, 0, 0, DateTimeKind.Utc)),
                new Operation(new Amount(200), new DateTime(2021, 2, 1, 0, 0, 0, DateTimeKind.Utc))
            ]);

        var statement = account.Statement();

        Amount[] expected = [new Amount(150), new Amount(200)];
        Assert.Equal(expected, statement.Select(line => line.Balance));
    }
```

Le test échoue : les opérations sont lues dans l'ordre de la liste.
<!-- /lang -->

<!-- lang:ts -->
Ajoutez ce test à `account.test.ts` :

```typescript
test("the statement sorts the operations by date, whatever their order", () => {
  const account = new Account(id, [
    { amount: new Amount(-50), date: new Date("2021-02-02T00:00:00Z") },
    { amount: new Amount(200), date: new Date("2021-02-01T00:00:00Z") }
  ]);

  const statement = account.statement();

  expect(statement.map((line) => line.balance)).toEqual([new Amount(150), new Amount(200)]);
});
```

Le test échoue : les opérations sont lues dans l'ordre de la liste.
<!-- /lang -->

**Vert**

<!-- lang:csharp -->
Dans `Statement`, parcourez les opérations triées :

```csharp
        foreach (var operation in _operations.OrderBy(operation => operation.Date))
```

Les 6 tests passent.
<!-- /lang -->

<!-- lang:ts -->
Dans `statement`, parcourez les opérations triées :

```typescript
    for (const operation of this.operations.toSorted((left, right) => left.date.getTime() - right.date.getTime())) {
```

Les 6 tests passent.
<!-- /lang -->

### Le compte distingue son historique de ses nouvelles opérations

**Rouge**

L'historique d'un compte ne change jamais : une opération enregistrée le reste. Seules les opérations ajoutées depuis le chargement du compte sont à enregistrer, et c'est au compte de les connaître.

<!-- lang:csharp -->
Pour que le test compile, ajoutez à `Account` :

```csharp
    public IReadOnlyList<Operation> NewOperations => _operations;
```

Ajoutez ce test à `AccountTests` :

```csharp
    [Fact]
    public void NewOperations_ContainsOnlyWhatWasAddedToTheHistory()
    {
        var account = new Account(Id, [new Operation(new Amount(100), Date)]);

        account.Deposit(new Amount(50), Date);

        var operation = Assert.Single(account.NewOperations);
        Assert.Equal(new Operation(new Amount(50), Date), operation);
    }
```

Le test échoue : la liste contient aussi l'historique.
<!-- /lang -->

<!-- lang:ts -->
Ajoutez à `Account` un accesseur qui renvoie toutes les opérations pour l'instant :

```typescript
  get newOperations(): readonly Operation[] {
    return this.operations;
  }
```

Ajoutez ce test à `account.test.ts` :

```typescript
test("the new operations only contain what was added to the history", () => {
  const account = new Account(id, [{ amount: new Amount(100), date }]);

  account.deposit(new Amount(50), date);

  expect(account.newOperations).toEqual([{ amount: new Amount(50), date }]);
});
```

Le test échoue : la liste contient aussi l'historique.
<!-- /lang -->

**Vert**

<!-- lang:csharp -->
Remplacez le contenu de `Account`, avec une liste pour l'historique, en lecture seule, et une autre pour les nouvelles opérations :

```csharp
namespace Bank.Domain.Models;

public class Account(AccountId id, IReadOnlyList<Operation> history)
{
    private readonly List<Operation> _newOperations = [];

    public AccountId Id => id;

    public IReadOnlyList<Operation> History => history;

    public IReadOnlyList<Operation> NewOperations => _newOperations;

    public Amount Balance => AllOperations.Aggregate(Amount.Zero, (balance, operation) => balance + operation.Amount);

    private IEnumerable<Operation> AllOperations => history.Concat(_newOperations);

    public void Deposit(Amount amount, DateTime date)
    {
        _newOperations.Add(new Operation(amount, date));
    }

    public void Withdraw(Amount amount, DateTime date)
    {
        if (amount > Balance)
        {
            throw new InsufficientFundsException(Balance);
        }

        _newOperations.Add(new Operation(-amount, date));
    }

    public IReadOnlyList<StatementLine> Statement()
    {
        var lines = new List<StatementLine>();
        var balance = Amount.Zero;

        foreach (var operation in AllOperations.OrderBy(operation => operation.Date))
        {
            balance += operation.Amount;
            lines.Add(new StatementLine(operation.Date, operation.Amount, balance));
        }

        lines.Reverse();
        return lines;
    }
}
```

Les 7 tests passent.
<!-- /lang -->

<!-- lang:ts -->
Remplacez le contenu de `account.ts`, avec une liste pour l'historique, en lecture seule, et une autre pour les nouvelles opérations :

```typescript
import { Amount } from "./amount.ts";
import { InsufficientFundsError } from "./insufficientFundsError.ts";
import type { AccountId } from "./accountId.ts";
import type { Operation } from "./operation.ts";
import type { StatementLine } from "./statementLine.ts";

export class Account {
  readonly id: AccountId;
  readonly history: readonly Operation[];
  private readonly addedOperations: Operation[] = [];

  constructor(id: AccountId, history: readonly Operation[]) {
    this.id = id;
    this.history = history;
  }

  get newOperations(): readonly Operation[] {
    return this.addedOperations;
  }

  get balance(): Amount {
    return this.allOperations.reduce((balance, operation) => balance.plus(operation.amount), Amount.zero);
  }

  private get allOperations(): Operation[] {
    return [...this.history, ...this.addedOperations];
  }

  deposit(amount: Amount, date: Date): void {
    this.addedOperations.push({ amount, date });
  }

  withdraw(amount: Amount, date: Date): void {
    if (amount.isGreaterThan(this.balance)) {
      throw new InsufficientFundsError(this.balance);
    }

    this.addedOperations.push({ amount: amount.negated(), date });
  }

  statement(): StatementLine[] {
    const lines: StatementLine[] = [];
    let balance = Amount.zero;

    for (const operation of this.allOperations.toSorted((left, right) => left.date.getTime() - right.date.getTime())) {
      balance = balance.plus(operation.amount);
      lines.push({ date: operation.date, amount: operation.amount, balance });
    }

    return lines.reverse();
  }
}
```

Les 7 tests passent.
<!-- /lang -->

---

## Le service du compte

Les règles sont en place. Il reste à relier le domaine à l'extérieur : ce dont il a besoin, décrit par les ports sortants, et ce qu'il propose, décrit par les ports entrants. Un service implémente les ports entrants à l'aide des ports sortants.

### Les ports sortants et leurs adapters en mémoire

Le domaine décrit ce dont il a besoin, avec son propre vocabulaire. Pour les tests, on écrit nous-mêmes des adapters en mémoire, sans framework de mock.

<!-- lang:csharp -->
Dans `src/Bank.Domain/Ports` :

`IOperations.cs`

```csharp
using Bank.Domain.Models;

namespace Bank.Domain.Ports;

public interface IOperations
{
    Task<Account> AccountOfAsync(AccountId accountId);

    Task SaveAsync(Account account);
}
```

`IClock.cs`

```csharp
namespace Bank.Domain.Ports;

public interface IClock
{
    DateTime Now();
}
```

Dans `tests/Bank.Domain.Tests` :

`InMemoryOperations.cs`

```csharp
using Bank.Domain.Models;
using Bank.Domain.Ports;

namespace Bank.Domain.Tests;

public class InMemoryOperations : IOperations
{
    private readonly Dictionary<AccountId, IReadOnlyList<Operation>> _operations = [];

    public Task<Account> AccountOfAsync(AccountId accountId)
    {
        var operations = _operations.GetValueOrDefault(accountId, []);
        return Task.FromResult(new Account(accountId, operations));
    }

    public Task SaveAsync(Account account)
    {
        _operations[account.Id] = [.. _operations.GetValueOrDefault(account.Id, []), .. account.NewOperations];
        return Task.CompletedTask;
    }
}
```

`FixedClock.cs`

```csharp
using Bank.Domain.Ports;

namespace Bank.Domain.Tests;

public class FixedClock(DateTime now) : IClock
{
    public DateTime Now() => now;
}
```
<!-- /lang -->

<!-- lang:ts -->
Dans `src/ports` :

`operations.ts`

```typescript
import type { Account } from "../models/account.ts";
import type { AccountId } from "../models/accountId.ts";

export interface Operations {
  accountOf(accountId: AccountId): Promise<Account>;

  save(account: Account): Promise<void>;
}
```

`clock.ts`

```typescript
export interface Clock {
  now(): Date;
}
```

Dans `tests` :

`inMemoryOperations.ts`

```typescript
import { Account } from "../src/models/account.ts";
import type { AccountId } from "../src/models/accountId.ts";
import type { Operation } from "../src/models/operation.ts";
import type { Operations } from "../src/ports/operations.ts";

export class InMemoryOperations implements Operations {
  private readonly operations = new Map<number, readonly Operation[]>();

  async accountOf(accountId: AccountId): Promise<Account> {
    return new Account(accountId, this.operations.get(accountId.value) ?? []);
  }

  async save(account: Account): Promise<void> {
    const operations = this.operations.get(account.id.value) ?? [];

    this.operations.set(account.id.value, [...operations, ...account.newOperations]);
  }
}
```

`fixedClock.ts`

```typescript
import type { Clock } from "../src/ports/clock.ts";

export class FixedClock implements Clock {
  private readonly date: Date;

  constructor(date: Date) {
    this.date = date;
  }

  now(): Date {
    return this.date;
  }
}
```
<!-- /lang -->

### Les ports entrants

Les ports entrants décrivent ce que l'application propose au monde extérieur, avec un port par fonctionnalité. Pour les nommer, on reprend la convention d'Alistair Cockburn : "for" suivi de l'intention, soit "pour déposer", "pour retirer" et "pour imprimer un relevé".

Pour l'instant, seuls les tests les appellent : on construit un hexagone neuf, et aucun autre adapter entrant n'existe. Les tests sont un adapter comme les autres : ils pilotent l'application par ces ports, et ne connaissent le service qu'au moment de le créer. Le contrôleur et l'écran du DAB les rejoindront le troisième jour.

<!-- lang:csharp -->
Dans `src/Bank.Domain/Ports` :

`IForDepositing.cs`

```csharp
using Bank.Domain.Models;

namespace Bank.Domain.Ports;

public interface IForDepositing
{
    Task DepositAsync(AccountId accountId, Amount amount);
}
```

`IForWithdrawing.cs`

```csharp
using Bank.Domain.Models;

namespace Bank.Domain.Ports;

public interface IForWithdrawing
{
    Task WithdrawAsync(AccountId accountId, Amount amount);
}
```

`IForPrintingStatements.cs`

```csharp
using Bank.Domain.Models;

namespace Bank.Domain.Ports;

public interface IForPrintingStatements
{
    Task<IReadOnlyList<StatementLine>> PrintStatementAsync(AccountId accountId);
}
```
<!-- /lang -->

<!-- lang:ts -->
Dans `src/ports` :

`forDepositing.ts`

```typescript
import type { AccountId } from "../models/accountId.ts";
import type { Amount } from "../models/amount.ts";

export interface ForDepositing {
  deposit(accountId: AccountId, amount: Amount): Promise<void>;
}
```

`forWithdrawing.ts`

```typescript
import type { AccountId } from "../models/accountId.ts";
import type { Amount } from "../models/amount.ts";

export interface ForWithdrawing {
  withdraw(accountId: AccountId, amount: Amount): Promise<void>;
}
```

`forPrintingStatements.ts`

```typescript
import type { AccountId } from "../models/accountId.ts";
import type { StatementLine } from "../models/statementLine.ts";

export interface ForPrintingStatements {
  printStatement(accountId: AccountId): Promise<StatementLine[]>;
}
```
<!-- /lang -->

### Déposer

**Rouge**

<!-- lang:csharp -->
Créez `tests/Bank.Domain.Tests/AccountServiceTests.cs`. Il ne compile pas : `AccountService` n'existe pas encore.

```csharp
using Bank.Domain.Models;
using Bank.Domain.Ports;
using Bank.Domain.Services;
using Xunit;

namespace Bank.Domain.Tests;

public class AccountServiceTests
{
    private static readonly AccountId Id = new(1);
    private static readonly DateTime Now = new(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryOperations _operations = new();
    private readonly IForDepositing _depositing;

    public AccountServiceTests()
    {
        _depositing = new AccountService(_operations, new FixedClock(Now));
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
<!-- /lang -->

<!-- lang:ts -->
Créez `tests/accountService.test.ts`. Il échoue : `AccountService` n'existe pas encore.

```typescript
import { beforeEach, expect, test } from "vitest";
import { AccountId } from "../src/models/accountId.ts";
import { Amount } from "../src/models/amount.ts";
import { AccountService } from "../src/services/accountService.ts";
import { FixedClock } from "./fixedClock.ts";
import { InMemoryOperations } from "./inMemoryOperations.ts";
import type { ForDepositing } from "../src/ports/forDepositing.ts";

const id = new AccountId(1);
const now = new Date("2021-01-10T00:00:00Z");

let operations: InMemoryOperations;
let depositing: ForDepositing;

beforeEach(() => {
  operations = new InMemoryOperations();
  depositing = new AccountService(operations, new FixedClock(now));
});

test("a deposit saves the operation at the current date", async () => {
  await depositing.deposit(id, new Amount(100));

  const account = await operations.accountOf(id);
  expect(account.history).toEqual([{ amount: new Amount(100), date: now }]);
});
```
<!-- /lang -->

**Vert**

<!-- lang:csharp -->
Créez `src/Bank.Domain/Services/AccountService.cs` :

```csharp
using Bank.Domain.Models;
using Bank.Domain.Ports;

namespace Bank.Domain.Services;

public class AccountService(IOperations operations, IClock clock) : IForDepositing
{
    public async Task DepositAsync(AccountId accountId, Amount amount)
    {
        var account = await operations.AccountOfAsync(accountId);
        account.Deposit(amount, clock.Now());
        await operations.SaveAsync(account);
    }
}
```

Les 8 tests passent. Comparez avec le premier jour : l'heure du test est exacte, et on n'encadre plus rien.
<!-- /lang -->

<!-- lang:ts -->
Créez `src/services/accountService.ts` :

```typescript
import type { AccountId } from "../models/accountId.ts";
import type { Amount } from "../models/amount.ts";
import type { Clock } from "../ports/clock.ts";
import type { ForDepositing } from "../ports/forDepositing.ts";
import type { Operations } from "../ports/operations.ts";

export class AccountService implements ForDepositing {
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

Les 8 tests passent. Comparez avec le premier jour : l'heure du test est exacte, et on n'encadre plus rien.
<!-- /lang -->

### Retirer

**Rouge**

<!-- lang:csharp -->
Dans `AccountServiceTests`, remplacez les champs des ports et le constructeur. Ils ne compilent pas : `AccountService` n'implémente pas encore `IForWithdrawing`.

```csharp
    private readonly IForDepositing _depositing;
    private readonly IForWithdrawing _withdrawing;

    public AccountServiceTests()
    {
        var service = new AccountService(_operations, new FixedClock(Now));
        _depositing = service;
        _withdrawing = service;
    }
```

Puis ajoutez ce test :

```csharp
    [Fact]
    public async Task Withdraw_SavesTheOperationAtTheCurrentDate()
    {
        await _depositing.DepositAsync(Id, new Amount(100));

        await _withdrawing.WithdrawAsync(Id, new Amount(30));

        var account = await _operations.AccountOfAsync(Id);
        Assert.Equal(new Operation(new Amount(-30), Now), account.History[^1]);
    }
```
<!-- /lang -->

<!-- lang:ts -->
Dans `accountService.test.ts`, importez le type `ForWithdrawing` et remplacez les variables et le `beforeEach`. `npm run typecheck` signale que `AccountService` n'implémente pas encore `ForWithdrawing`.

```typescript
let operations: InMemoryOperations;
let depositing: ForDepositing;
let withdrawing: ForWithdrawing;

beforeEach(() => {
  operations = new InMemoryOperations();
  const service = new AccountService(operations, new FixedClock(now));
  depositing = service;
  withdrawing = service;
});
```

Puis ajoutez ce test. Il échoue : `withdraw` n'existe pas encore.

```typescript
test("a withdrawal saves the operation at the current date", async () => {
  await depositing.deposit(id, new Amount(100));

  await withdrawing.withdraw(id, new Amount(30));

  const account = await operations.accountOf(id);
  expect(account.history.at(-1)).toEqual({ amount: new Amount(-30), date: now });
});
```
<!-- /lang -->

**Vert**

<!-- lang:csharp -->
Remplacez `AccountService` :

```csharp
using Bank.Domain.Models;
using Bank.Domain.Ports;

namespace Bank.Domain.Services;

public class AccountService(IOperations operations, IClock clock) : IForDepositing, IForWithdrawing
{
    public async Task DepositAsync(AccountId accountId, Amount amount)
    {
        var account = await operations.AccountOfAsync(accountId);
        account.Deposit(amount, clock.Now());
        await operations.SaveAsync(account);
    }

    public async Task WithdrawAsync(AccountId accountId, Amount amount)
    {
        var account = await operations.AccountOfAsync(accountId);
        account.Withdraw(amount, clock.Now());
        await operations.SaveAsync(account);
    }
}
```

Les 9 tests passent.
<!-- /lang -->

<!-- lang:ts -->
Remplacez `AccountService` :

```typescript
import type { AccountId } from "../models/accountId.ts";
import type { Amount } from "../models/amount.ts";
import type { Clock } from "../ports/clock.ts";
import type { ForDepositing } from "../ports/forDepositing.ts";
import type { ForWithdrawing } from "../ports/forWithdrawing.ts";
import type { Operations } from "../ports/operations.ts";

export class AccountService implements ForDepositing, ForWithdrawing {
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

  async withdraw(accountId: AccountId, amount: Amount): Promise<void> {
    const account = await this.operations.accountOf(accountId);
    account.withdraw(amount, this.clock.now());
    await this.operations.save(account);
  }
}
```

Les 9 tests passent.
<!-- /lang -->

### Consulter le relevé

**Rouge**

<!-- lang:csharp -->
Remplacez de nouveau les champs des ports et le constructeur. Ils ne compilent pas : `AccountService` n'implémente pas encore `IForPrintingStatements`.

```csharp
    private readonly IForDepositing _depositing;
    private readonly IForWithdrawing _withdrawing;
    private readonly IForPrintingStatements _printingStatements;

    public AccountServiceTests()
    {
        var service = new AccountService(_operations, new FixedClock(Now));
        _depositing = service;
        _withdrawing = service;
        _printingStatements = service;
    }
```

Puis ajoutez ce test :

```csharp
    [Fact]
    public async Task PrintStatement_ReturnsTheStatementOfTheAccount()
    {
        await _depositing.DepositAsync(Id, new Amount(100));

        var statement = await _printingStatements.PrintStatementAsync(Id);

        var line = Assert.Single(statement);
        Assert.Equal(new StatementLine(Now, new Amount(100), new Amount(100)), line);
    }
```
<!-- /lang -->

<!-- lang:ts -->
Importez le type `ForPrintingStatements`, et remplacez de nouveau les variables et le `beforeEach`. `npm run typecheck` signale que `AccountService` n'implémente pas encore `ForPrintingStatements`.

```typescript
let operations: InMemoryOperations;
let depositing: ForDepositing;
let withdrawing: ForWithdrawing;
let printingStatements: ForPrintingStatements;

beforeEach(() => {
  operations = new InMemoryOperations();
  const service = new AccountService(operations, new FixedClock(now));
  depositing = service;
  withdrawing = service;
  printingStatements = service;
});
```

Puis ajoutez ce test. Il échoue : `printStatement` n'existe pas encore.

```typescript
test("printing a statement returns the statement of the account", async () => {
  await depositing.deposit(id, new Amount(100));

  const statement = await printingStatements.printStatement(id);

  expect(statement).toEqual([{ date: now, amount: new Amount(100), balance: new Amount(100) }]);
});
```
<!-- /lang -->

**Vert**

<!-- lang:csharp -->
Remplacez `AccountService` :

```csharp
using Bank.Domain.Models;
using Bank.Domain.Ports;

namespace Bank.Domain.Services;

public class AccountService(IOperations operations, IClock clock)
    : IForDepositing, IForWithdrawing, IForPrintingStatements
{
    public async Task DepositAsync(AccountId accountId, Amount amount)
    {
        var account = await operations.AccountOfAsync(accountId);
        account.Deposit(amount, clock.Now());
        await operations.SaveAsync(account);
    }

    public async Task WithdrawAsync(AccountId accountId, Amount amount)
    {
        var account = await operations.AccountOfAsync(accountId);
        account.Withdraw(amount, clock.Now());
        await operations.SaveAsync(account);
    }

    public async Task<IReadOnlyList<StatementLine>> PrintStatementAsync(AccountId accountId)
    {
        var account = await operations.AccountOfAsync(accountId);
        return account.Statement();
    }
}
```

Les 10 tests passent. Le service implémente les trois ports entrants, et n'a besoin que des deux ports sortants.
<!-- /lang -->

<!-- lang:ts -->
Remplacez `AccountService` :

```typescript
import type { AccountId } from "../models/accountId.ts";
import type { Amount } from "../models/amount.ts";
import type { StatementLine } from "../models/statementLine.ts";
import type { Clock } from "../ports/clock.ts";
import type { ForDepositing } from "../ports/forDepositing.ts";
import type { ForPrintingStatements } from "../ports/forPrintingStatements.ts";
import type { ForWithdrawing } from "../ports/forWithdrawing.ts";
import type { Operations } from "../ports/operations.ts";

export class AccountService implements ForDepositing, ForPrintingStatements, ForWithdrawing {
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

  async withdraw(accountId: AccountId, amount: Amount): Promise<void> {
    const account = await this.operations.accountOf(accountId);
    account.withdraw(amount, this.clock.now());
    await this.operations.save(account);
  }

  async printStatement(accountId: AccountId): Promise<StatementLine[]> {
    const account = await this.operations.accountOf(accountId);
    return account.statement();
  }
}
```

Les 10 tests passent. Le service implémente les trois ports entrants, et n'a besoin que des deux ports sortants.

### Ce que le domaine expose

Les autres paquets n'accèdent au domaine que par `src/index.ts` : c'est le champ `exports` de son `package.json`. Remplacez ce fichier :

```typescript
export { Account } from "./models/account.ts";
export { AccountId } from "./models/accountId.ts";
export { Amount } from "./models/amount.ts";
export { InsufficientFundsError } from "./models/insufficientFundsError.ts";
export type { Operation } from "./models/operation.ts";
export type { StatementLine } from "./models/statementLine.ts";
export type { Clock } from "./ports/clock.ts";
export type { ForDepositing } from "./ports/forDepositing.ts";
export type { ForPrintingStatements } from "./ports/forPrintingStatements.ts";
export type { ForWithdrawing } from "./ports/forWithdrawing.ts";
export type { Operations } from "./ports/operations.ts";
export { AccountService } from "./services/accountService.ts";
```
<!-- /lang -->

---

## Comparer avec hier

<!-- lang:csharp -->
Arrêtez la base, puis lancez les tests :

```bash
# Depuis la racine du dépôt
supabase stop

# Depuis jour-2/hexagon/csharp
dotnet test
```

Les 10 tests passent, sans base, sans configuration et sans rien à nettoyer. Redémarrez la base avec `supabase start` pour la suite.
<!-- /lang -->

<!-- lang:ts -->
Arrêtez la base, puis lancez les tests :

```bash
# Depuis la racine du dépôt
supabase stop

# Depuis jour-2/hexagon/ts
npm test
```

Les 10 tests passent, sans base, sans configuration et sans rien à nettoyer. Redémarrez la base avec `supabase start` pour la suite.
<!-- /lang -->

Le domaine n'est pas encore branché : l'API utilise toujours le service d'hier. C'est le travail du troisième jour.
