<!-- Généré depuis solution.src.md. Modifiez la source, pas ce fichier. -->

# Jour 1 : Solution (TypeScript)

Ce guide reprend pas à pas les étapes 2 et 3 du premier jour. Il part du projet de `jour-1/start/`.

## Avant de commencer

La base doit être démarrée, et le projet doit lancer son test sans erreur :

```bash
# Depuis la racine du dépôt
supabase start
supabase db reset

# Depuis jour-1/start/ts
npm install
npm test
```

Si la commande échoue sur `.env`, suivez le README du projet pour créer ce fichier.

---

## Étape 2 : Tester le relevé

On veut figer le comportement du relevé en testant la règle métier, donc en appelant directement le service. Le service expose déjà tout ce qu'il faut : le relevé, le dépôt et le retrait.

### Le test du projet de départ

Faites un dépôt sur le compte 1, avec l'API démarrée, puis relancez le test du projet :

```bash
curl -X POST http://localhost:5122/accounts/1/deposits \
  -H "Content-Type: application/json" \
  -d '{ "amount": 100 }'

npm test
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

Ce sont `beforeEach` et `afterEach` qui s'en chargent : Vitest les appelle autour de chaque test, et `afterEach` s'exécute même si le test échoue.

Ce nettoyage suppose que deux tests n'utilisent jamais ce compte en même temps.

Ici, les tests d'un même fichier s'exécutent l'un après l'autre. Un second fichier de tests devrait utiliser un autre compte, car les fichiers s'exécutent en parallèle.

### Les tests

Créez `tests/accountService.test.ts` :

```typescript
import { createClient } from "@supabase/supabase-js";
import { afterEach, beforeEach, expect, test } from "vitest";
import { AccountService } from "../src/business/accountService.ts";

const accountId = 42;

async function clearOperations(): Promise<void> {
  const client = createClient(process.env.SUPABASE_URL!, process.env.SUPABASE_KEY!);

  const { error } = await client.from("operations").delete().eq("account_id", accountId);

  if (error) {
    throw error;
  }
}

beforeEach(clearOperations);
afterEach(clearOperations);

test("the statement is empty when the account has no operations", async () => {
  const service = new AccountService(process.env);

  const statement = await service.getStatement(accountId);

  expect(statement).toEqual([]);
});

test("the statement lists the operations, newest first, with the running balance", async () => {
  const service = new AccountService(process.env);
  await service.deposit(accountId, 1000);
  await service.deposit(accountId, 2000);
  await service.withdraw(accountId, 500);

  const statement = await service.getStatement(accountId);

  expect(statement.map((line) => line.amount)).toEqual([-500, 2000, 1000]);
  expect(statement.map((line) => line.balance)).toEqual([2500, 3000, 1000]);
});

test("the statement dates the operations with the current time", async () => {
  const service = new AccountService(process.env);
  const before = Date.now();

  await service.deposit(accountId, 1000);

  const [line] = await service.getStatement(accountId);
  expect(line.date.getTime()).toBeGreaterThanOrEqual(before - 1000);
  expect(line.date.getTime()).toBeLessThanOrEqual(Date.now());
});

test("a withdrawal is accepted even without funds", async () => {
  const service = new AccountService(process.env);

  await service.withdraw(accountId, 100);

  const statement = await service.getStatement(accountId);
  expect(statement).toHaveLength(1);
  expect(statement[0].balance).toBe(-100);
});
```

Lancez les tests avec `npm test` : les 5 tests passent, dont celui du projet de départ.

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

Pour que le test compile, créez `src/business/insufficientFundsError.ts` :

```typescript
export class InsufficientFundsError extends Error {
  constructor(balance: number) {
    super(`Insufficient funds: the balance is ${balance}.`);
  }
}
```

Dans `tests/accountService.test.ts`, importez `InsufficientFundsError`, et remplacez le test du retrait sans provision par :

```typescript
test("a withdrawal is refused when above the balance", async () => {
  const service = new AccountService(process.env);
  await service.deposit(accountId, 100);

  await expect(service.withdraw(accountId, 100.01)).rejects.toThrow(InsufficientFundsError);

  const statement = await service.getStatement(accountId);
  expect(statement).toHaveLength(1);
  expect(statement[0].balance).toBe(100);
});
```

Lancez `npm test` : le test échoue, car `withdraw` ne rejette pas la promesse.

**Vert**

Dans `AccountService`, importez `InsufficientFundsError`, et vérifiez le solde avant d'enregistrer le retrait :

```typescript
  async withdraw(accountId: number, amount: number): Promise<void> {
    const operations = await this.repository.getOperations(accountId);
    const balance = operations.reduce((sum, operation) => sum + operation.amount, 0);

    if (amount > balance) {
      throw new InsufficientFundsError(balance);
    }

    await this.repository.insertOperation(accountId, -amount, new Date());
  }
```

Lancez `npm test` : les 5 tests passent.

#### Deuxième test : accepter un retrait égal au solde

Ce cas est déjà couvert par le cycle précédent : le test passera du premier coup, et il ne pilote aucun code. On sort donc du TDD le temps d'un test, pour fixer ce comportement à la limite.

Ajoutez ce test :

```typescript
test("a withdrawal is accepted when equal to the balance", async () => {
  const service = new AccountService(process.env);
  await service.deposit(accountId, 100);

  await service.withdraw(accountId, 100);

  const statement = await service.getStatement(accountId);
  expect(statement[0].balance).toBe(0);
});
```

Un test qui n'a jamais échoué ne prouve rien. Vérifiez qu'il peut échouer en remplaçant temporairement `amount > balance` par `amount >= balance` dans le service : le test échoue. Remettez `amount > balance`. Lancez `npm test` : les 6 tests passent.

#### Traduire le refus dans l'API

L'API doit encore traduire ce refus. Dans `accountsController.ts`, importez `InsufficientFundsError`, puis remplacez la route du retrait :

```typescript
  router.post("/accounts/:accountId/withdrawals", async (request: Request, response: Response) => {
    const { amount } = request.body as AmountRequest;

    try {
      await accountService.withdraw(Number(request.params.accountId), amount);
      response.sendStatus(201);
    } catch (error) {
      if (error instanceof InsufficientFundsError) {
        response.status(422).send(error.message);
      } else {
        throw error;
      }
    }
  });
```

Vérifiez-le à la main, avec l'API démarrée :

```bash
curl -X POST http://localhost:5122/accounts/1/withdrawals \
  -H "Content-Type: application/json" \
  -d '{ "amount": 100000 }'
```

L'API répond `422` avec le message de l'erreur.

### Partie 2 : Avec des Seams

Pour se passer de la base et contrôler l'heure, on crée un point de substitution pour chaque dépendance du service.

À la fin de `AccountService`, faites passer chaque accès à l'extérieur par une méthode qu'on peut redéfinir, et importez le type `OperationRecord` :

```typescript
  protected getOperations(accountId: number): Promise<OperationRecord[]> {
    return this.repository.getOperations(accountId);
  }

  protected insertOperation(accountId: number, amount: number, date: Date): Promise<void> {
    return this.repository.insertOperation(accountId, amount, date);
  }

  protected now(): Date {
    return new Date();
  }
```

Puis utilisez ces méthodes dans le reste de la classe : `await this.getOperations(accountId)` à la place de `await this.repository.getOperations(accountId)`, `await this.insertOperation(accountId, amount, this.now())` dans le dépôt, et `await this.insertOperation(accountId, -amount, this.now())` dans le retrait.

Créez `tests/accountService.seams.test.ts` :

```typescript
import { expect, test } from "vitest";
import { AccountService } from "../src/business/accountService.ts";
import { InsufficientFundsError } from "../src/business/insufficientFundsError.ts";
import type { OperationRecord } from "../src/data/operationRecord.ts";

const accountId = 42;

class TestableAccountService extends AccountService {
  private readonly operations: OperationRecord[] = [];

  currentDate = new Date();

  constructor() {
    super({ SUPABASE_URL: "http://unused", SUPABASE_KEY: "unused" });
  }

  protected override async getOperations(): Promise<OperationRecord[]> {
    return this.operations;
  }

  protected override async insertOperation(accountId: number, amount: number, date: Date): Promise<void> {
    this.operations.push({
      id: this.operations.length + 1,
      account_id: accountId,
      amount,
      date: date.toISOString()
    });
  }

  protected override now(): Date {
    return this.currentDate;
  }
}

test("the statement matches the kata example", async () => {
  const service = new TestableAccountService();
  service.currentDate = new Date("2021-01-10T00:00:00Z");
  await service.deposit(accountId, 1000);
  service.currentDate = new Date("2021-01-15T00:00:00Z");
  await service.deposit(accountId, 2000);
  service.currentDate = new Date("2021-01-20T00:00:00Z");
  await service.withdraw(accountId, 500);

  const statement = await service.getStatement(accountId);

  expect(statement).toEqual([
    { date: new Date("2021-01-20T00:00:00Z"), amount: -500, balance: 2500 },
    { date: new Date("2021-01-15T00:00:00Z"), amount: 2000, balance: 3000 },
    { date: new Date("2021-01-10T00:00:00Z"), amount: 1000, balance: 1000 }
  ]);
});

test("a withdrawal is refused when above the balance", async () => {
  const service = new TestableAccountService();
  await service.deposit(accountId, 100);

  await expect(service.withdraw(accountId, 100.01)).rejects.toThrow(InsufficientFundsError);

  const statement = await service.getStatement(accountId);
  expect(statement).toHaveLength(1);
  expect(statement[0].balance).toBe(100);
});
```

Lancez `npm test` : les 8 tests passent, dont 2 sans la base.

On peut enfin reproduire l'exemple du relevé, avec ses dates. Mais observez ce que cela a demandé :

- Quatre Seams pour une règle de quelques lignes : la lecture, l'écriture, l'heure et la configuration, car le service ne peut pas être créé sans elle
- Une classe de test plus longue que la règle qu'elle vérifie
- Des méthodes ouvertes à la redéfinition uniquement pour les tests
- Un faux stockage qui renvoie les opérations dans leur ordre d'insertion : le tri de la vraie requête n'est plus testé
