<!-- Généré depuis solution.src.md. Modifiez la source, pas ce fichier. -->

# Jour 2 : Solution (C#)

Ce guide reprend pas à pas la construction du domaine en TDD. Il part du projet vide de `jour-2/hexagon/`.

## Le projet de départ

Le domaine vit dans son propre module, qui ne connaît ni l'API, ni la base. C'est le compilateur qui le garantit : ce module ne référence rien.

`jour-2/hexagon/csharp` contient `Bank.Domain`, vide, et son projet de tests. Lancez les tests depuis ce dossier :

```bash
dotnet test
```

Aucun test n'est trouvé pour l'instant : c'est normal, et la commande se termine sans erreur.

---

## Les règles du compte

On avance un test à la fois. Ici, plus besoin de configuration, de données à nettoyer ou de compte réservé : un test crée un compte en mémoire, et c'est tout.

### Un dépôt augmente le solde

Le montant et l'identifiant du compte ne sont pas de simples nombres : ce sont des **value objects**. Ils sont définis par leur valeur, sans identité propre : deux montants de 100 sont le même montant, alors que deux comptes au même solde restent deux comptes. On ne confond plus un montant avec un identifiant, et le calcul sur les montants a sa place.

**Rouge**

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

**Vert**

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

### Un retrait diminue le solde

**Rouge**

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

**Vert**

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

### Un retrait supérieur au solde est refusé

**Rouge**

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

**Vert**

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

### Un retrait égal au solde est accepté

Ce cas est déjà couvert par le cycle précédent : le test passera du premier coup, et il ne pilote aucun code. On sort donc du TDD le temps d'un test, pour fixer ce comportement à la limite.

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

### Le relevé

**Rouge**

C'est l'exemple du premier jour, avec ses dates. Hier, aucun test ne pouvait le reproduire : l'heure venait du service. Ici, la date fait partie des paramètres.

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

**Vert**

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

### Le relevé trie les opérations par date

**Rouge**

Hier, ce tri n'était pas vraiment testé. Il suffit ici de créer un compte dont les opérations ne sont pas dans l'ordre.

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

**Vert**

Dans `Statement`, parcourez les opérations triées :

```csharp
        foreach (var operation in _operations.OrderBy(operation => operation.Date))
```

Les 6 tests passent.

### Le compte distingue son historique de ses nouvelles opérations

**Rouge**

L'historique d'un compte ne change jamais : une opération enregistrée le reste. Seules les opérations ajoutées depuis le chargement du compte sont à enregistrer, et c'est au compte de les connaître.

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

**Vert**

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

---

## Le service du compte

Les règles sont en place. Il reste à relier le domaine à l'extérieur : ce dont il a besoin, décrit par les ports sortants, et ce qu'il propose, décrit par les ports entrants. Un service implémente les ports entrants à l'aide des ports sortants.

### Les ports sortants et leurs adapters en mémoire

Le domaine décrit ce dont il a besoin, avec son propre vocabulaire. Pour les tests, on écrit nous-mêmes des adapters en mémoire, sans framework de mock.

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

### Les ports entrants

Les ports entrants décrivent ce que l'application propose au monde extérieur, avec un port par fonctionnalité. Pour les nommer, on reprend la convention d'Alistair Cockburn : "for" suivi de l'intention, soit "pour déposer", "pour retirer" et "pour imprimer un relevé".

Pour l'instant, seuls les tests les appellent : on construit un hexagone neuf, et aucun autre adapter entrant n'existe. Les tests sont un adapter comme les autres : ils pilotent l'application par ces ports, et ne connaissent le service qu'au moment de le créer. Le contrôleur et l'écran du DAB les rejoindront le troisième jour.

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

### Déposer

**Rouge**

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

**Vert**

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

### Retirer

**Rouge**

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

**Vert**

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

### Consulter le relevé

**Rouge**

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

**Vert**

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

---

## Comparer avec hier

Arrêtez la base, puis lancez les tests :

```bash
# Depuis la racine du dépôt
supabase stop

# Depuis jour-2/hexagon/csharp
dotnet test
```

Les 10 tests passent, sans base, sans configuration et sans rien à nettoyer. Redémarrez la base avec `supabase start` pour la suite.

Le domaine n'est pas encore branché : l'API utilise toujours le service d'hier. C'est le travail du troisième jour.
