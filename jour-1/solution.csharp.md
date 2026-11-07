<!-- Généré depuis solution.src.md. Modifiez la source, pas ce fichier. -->

# Jour 1 : Solution (C#)

Ce guide reprend pas à pas les étapes 2 et 3 du premier jour. Il part du projet de `jour-1/start/`.

## Avant de commencer

La base doit être démarrée, et le projet doit lancer son test sans erreur :

```bash
# Depuis la racine du dépôt
supabase start
supabase db reset

# Depuis jour-1/start/csharp
dotnet test
```

Si le test échoue sur `appsettings.local.json`, suivez le README du projet pour créer ce fichier.

---

## Étape 2 : Tester le relevé

On veut figer le comportement du relevé en testant la règle métier, donc en appelant directement le service. Le service expose déjà tout ce qu'il faut : le relevé, le dépôt et le retrait.

### Le test du projet de départ

Faites un dépôt sur le compte 1, avec l'API démarrée, puis relancez le test du projet :

```bash
curl -X POST http://localhost:5122/accounts/1/deposits \
  -H "Content-Type: application/json" \
  -d '{ "amount": 100 }'

dotnet test
```

Le test échoue, alors que le code n'a pas changé : il dépend de ce qui se trouve dans la base au moment où il s'exécute. C'est pour cela que les tests qui suivent créent leurs propres données. Remettez la base dans son état initial avec `supabase db reset`.

### Créer le service

Le service crée lui-même son repository : impossible de lui en donner un autre. Pour l'instancier, il faut donc une vraie configuration, avec l'adresse et la clé de la vraie base.

### Préparer les données

Chaque test crée ses propres données, en appelant le dépôt et le retrait du service. Il ne compte pas sur `supabase/seed.sql` : si quelqu'un modifie ces données, les tests continuent de passer, et on lit dans le test tout ce dont il a besoin.

### Nettoyer la base

Les tests partagent une seule base, et ce qu'un test y écrit y reste. On réserve donc un compte aux tests, qu'on vide à deux moments :

- **Avant chaque test** : c'est ce qui garantit qu'un test part d'un compte vide, même si l'exécution précédente a été interrompue avant de nettoyer
- **Après chaque test** : pour ne rien laisser en base, y compris quand le test échoue

Le repository ne sait pas supprimer d'opérations, et on ne va pas lui ajouter une méthode pour les tests : le nettoyage passe directement par la base.

C'est `IAsyncLifetime` qui s'en charge : xUnit crée une nouvelle instance de la classe pour chaque test, appelle `InitializeAsync` avant le test et `DisposeAsync` après, même s'il échoue. `IClassFixture` n'y joue aucun rôle : il partage la même `WebApplicationFactory` entre les tests de la classe.

Ce nettoyage suppose que deux tests n'utilisent jamais ce compte en même temps.

Ici, les tests d'une même classe s'exécutent l'un après l'autre. Une seconde classe de tests devrait utiliser un autre compte, car les classes s'exécutent en parallèle.

### Les tests

Créez `tests/Bank.Tests/AccountServiceTests.cs` :

```csharp
using Bank.Business;
using Bank.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supabase;

namespace Bank.Tests;

public class AccountServiceTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const int AccountId = 42;

    private readonly IConfiguration _configuration = factory.Services.GetRequiredService<IConfiguration>();

    public async ValueTask InitializeAsync() => await ClearOperationsAsync();

    public async ValueTask DisposeAsync() => await ClearOperationsAsync();

    [Fact]
    public async Task Statement_IsEmpty_WhenTheAccountHasNoOperations()
    {
        var service = new AccountService(_configuration);

        var statement = await service.GetStatementAsync(AccountId);

        Assert.Empty(statement);
    }

    [Fact]
    public async Task Statement_ListsOperationsNewestFirstWithRunningBalance()
    {
        var service = new AccountService(_configuration);
        await service.DepositAsync(AccountId, 1000);
        await service.DepositAsync(AccountId, 2000);
        await service.WithdrawAsync(AccountId, 500);

        var statement = await service.GetStatementAsync(AccountId);

        Assert.Equal([-500m, 2000m, 1000m], statement.Select(line => line.Amount));
        Assert.Equal([2500m, 3000m, 1000m], statement.Select(line => line.Balance));
    }

    [Fact]
    public async Task Statement_DatesOperationsWithTheCurrentTime()
    {
        var service = new AccountService(_configuration);
        var before = DateTime.UtcNow;

        await service.DepositAsync(AccountId, 1000);

        var line = Assert.Single(await service.GetStatementAsync(AccountId));
        Assert.InRange(line.Date, before.AddSeconds(-1), DateTime.UtcNow);
    }

    [Fact]
    public async Task Withdrawal_IsAccepted_EvenWithoutFunds()
    {
        var service = new AccountService(_configuration);

        await service.WithdrawAsync(AccountId, 100);

        var line = Assert.Single(await service.GetStatementAsync(AccountId));
        Assert.Equal(-100, line.Balance);
    }

    private async Task ClearOperationsAsync()
    {
        var client = new Client(_configuration["Supabase:Url"]!, _configuration["Supabase:Key"]!);

        await client.From<OperationRecord>()
            .Where(operation => operation.AccountId == AccountId)
            .Delete();
    }
}
```

Lancez les tests avec `dotnet test` : les 5 tests passent, dont celui du projet de départ.

### Ce que l'heure système empêche

Le service prend l'heure lui-même, et un test ne peut pas la contrôler. Regardez ce qui en découle :

- **On ne peut pas reproduire l'exemple du relevé.** Ses opérations datent de janvier 2021, alors qu'un dépôt est toujours daté de maintenant. Le test vérifie les montants et les soldes, jamais les dates.
- **Une date se vérifie à peu près.** Le test ne peut que l'encadrer entre deux instants.
- **Le tri par date n'est pas vraiment testé.** Les opérations sont créées dans l'ordre chronologique, à quelques millisecondes d'écart. Le test ne dit rien d'une opération plus ancienne qui serait enregistrée après une plus récente.

Un dernier test mérite qu'on s'y arrête : celui du retrait sans provision fige un comportement qu'on sait incorrect. C'est le rôle d'un test de caractérisation : il décrit ce que le code fait aujourd'hui, pas ce qu'il devrait faire.

---

## Étape 3 : La règle du solde

> Un utilisateur ne peut pas retirer plus que le solde disponible.

On procède en deux temps : d'abord avec la vraie base, puis avec des Seams. La première partie ne permet pas de vérifier précisément les dates du relevé : la seconde y répond.

### Partie 1 : Avec la vraie base

La règle contredit le test du retrait sans provision : c'est normal, on change un comportement volontairement. On avance un test à la fois, et chaque test passe par un cycle complet.

#### Premier test : refuser un retrait supérieur au solde

**Rouge**

Pour que le test compile, créez `src/Bank.Business/InsufficientFundsException.cs` :

```csharp
namespace Bank.Business;

public class InsufficientFundsException(decimal balance)
    : Exception($"Insufficient funds: the balance is {balance}.");
```

Dans `AccountServiceTests`, remplacez `Withdrawal_IsAccepted_EvenWithoutFunds` par :

```csharp
    [Fact]
    public async Task Withdrawal_IsRefused_WhenAboveTheBalance()
    {
        var service = new AccountService(_configuration);
        await service.DepositAsync(AccountId, 100);

        await Assert.ThrowsAsync<InsufficientFundsException>(() => service.WithdrawAsync(AccountId, 100.01m));

        var line = Assert.Single(await service.GetStatementAsync(AccountId));
        Assert.Equal(100, line.Balance);
    }
```

Lancez `dotnet test` : le test échoue, car aucune exception n'est levée.

**Vert**

Dans `AccountService`, vérifiez le solde avant d'enregistrer le retrait :

```csharp
    public async Task WithdrawAsync(int accountId, decimal amount)
    {
        var operations = await _repository.GetOperationsAsync(accountId);
        var balance = operations.Sum(operation => operation.Amount);

        if (amount > balance)
        {
            throw new InsufficientFundsException(balance);
        }

        await _repository.InsertOperationAsync(accountId, -amount, DateTime.UtcNow);
    }
```

Lancez `dotnet test` : les 5 tests passent.

#### Deuxième test : accepter un retrait égal au solde

Ce cas est déjà couvert par le cycle précédent : le test passera du premier coup, et il ne pilote aucun code. On sort donc du TDD le temps d'un test, pour fixer ce comportement à la limite.

Ajoutez ce test :

```csharp
    [Fact]
    public async Task Withdrawal_IsAccepted_WhenEqualToTheBalance()
    {
        var service = new AccountService(_configuration);
        await service.DepositAsync(AccountId, 100);

        await service.WithdrawAsync(AccountId, 100);

        var statement = await service.GetStatementAsync(AccountId);
        Assert.Equal(0, statement[0].Balance);
    }
```

Un test qui n'a jamais échoué ne prouve rien. Vérifiez qu'il peut échouer en remplaçant temporairement `amount > balance` par `amount >= balance` dans le service : le test échoue. Remettez `amount > balance`. Lancez `dotnet test` : les 6 tests passent.

#### Traduire le refus dans l'API

L'API doit encore traduire ce refus. Dans `AccountsController` :

```csharp
    [HttpPost("withdrawals")]
    public async Task<IActionResult> Withdraw(int accountId, AmountRequest request)
    {
        try
        {
            await accountService.WithdrawAsync(accountId, request.Amount);
            return Created();
        }
        catch (InsufficientFundsException exception)
        {
            return UnprocessableEntity(exception.Message);
        }
    }
```

Vérifiez-le à la main, avec l'API démarrée :

```bash
curl -X POST http://localhost:5122/accounts/1/withdrawals \
  -H "Content-Type: application/json" \
  -d '{ "amount": 100000 }'
```

L'API répond `422` avec le message de l'exception.

### Partie 2 : Avec des Seams

Pour se passer de la base et contrôler l'heure, on crée un point de substitution pour chaque dépendance du service.

À la fin de `AccountService`, faites passer chaque accès à l'extérieur par une méthode qu'on peut redéfinir :

```csharp
    protected virtual Task<IReadOnlyList<OperationRecord>> GetOperationsAsync(int accountId) =>
        _repository.GetOperationsAsync(accountId);

    protected virtual Task InsertOperationAsync(int accountId, decimal amount, DateTime date) =>
        _repository.InsertOperationAsync(accountId, amount, date);

    protected virtual DateTime Now() => DateTime.UtcNow;
```

Puis utilisez ces méthodes dans le reste de la classe : `await GetOperationsAsync(accountId)` à la place de `await _repository.GetOperationsAsync(accountId)`, `await InsertOperationAsync(accountId, amount, Now())` dans le dépôt, et `await InsertOperationAsync(accountId, -amount, Now())` dans le retrait.

Créez `tests/Bank.Tests/AccountServiceSeamTests.cs` :

```csharp
using Bank.Business;
using Bank.Data;
using Microsoft.Extensions.Configuration;

namespace Bank.Tests;

public class AccountServiceSeamTests
{
    private const int AccountId = 42;

    [Fact]
    public async Task Statement_MatchesTheKataExample()
    {
        var service = new TestableAccountService();
        service.CurrentDate = new DateTime(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        await service.DepositAsync(AccountId, 1000);
        service.CurrentDate = new DateTime(2021, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        await service.DepositAsync(AccountId, 2000);
        service.CurrentDate = new DateTime(2021, 1, 20, 0, 0, 0, DateTimeKind.Utc);
        await service.WithdrawAsync(AccountId, 500);

        var statement = await service.GetStatementAsync(AccountId);

        StatementLine[] expected =
        [
            new(new DateTime(2021, 1, 20, 0, 0, 0, DateTimeKind.Utc), -500, 2500),
            new(new DateTime(2021, 1, 15, 0, 0, 0, DateTimeKind.Utc), 2000, 3000),
            new(new DateTime(2021, 1, 10, 0, 0, 0, DateTimeKind.Utc), 1000, 1000)
        ];
        Assert.Equal(expected, statement);
    }

    [Fact]
    public async Task Withdrawal_IsRefused_WhenAboveTheBalance()
    {
        var service = new TestableAccountService();
        await service.DepositAsync(AccountId, 100);

        await Assert.ThrowsAsync<InsufficientFundsException>(() => service.WithdrawAsync(AccountId, 100.01m));

        var line = Assert.Single(await service.GetStatementAsync(AccountId));
        Assert.Equal(100, line.Balance);
    }

    private class TestableAccountService() : AccountService(FakeConfiguration)
    {
        private static readonly IConfiguration FakeConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supabase:Url"] = "http://unused",
                ["Supabase:Key"] = "unused"
            })
            .Build();

        private readonly List<OperationRecord> _operations = [];

        public DateTime CurrentDate { get; set; }

        protected override Task<IReadOnlyList<OperationRecord>> GetOperationsAsync(int accountId) =>
            Task.FromResult<IReadOnlyList<OperationRecord>>(_operations);

        protected override Task InsertOperationAsync(int accountId, decimal amount, DateTime date)
        {
            _operations.Add(new OperationRecord { AccountId = accountId, Amount = amount, Date = date });
            return Task.CompletedTask;
        }

        protected override DateTime Now() => CurrentDate;
    }
}
```

Lancez `dotnet test` : les 8 tests passent, dont 2 sans la base.

On peut enfin reproduire l'exemple du relevé, avec ses dates. Mais observez ce que cela a demandé :

- Quatre Seams pour une règle de quelques lignes : la lecture, l'écriture, l'heure et la configuration, car le service ne peut pas être créé sans elle
- Une classe de test plus longue que la règle qu'elle vérifie
- Des méthodes ouvertes à la redéfinition uniquement pour les tests
- Un faux stockage qui renvoie les opérations dans leur ordre d'insertion : le tri de la vraie requête n'est plus testé
