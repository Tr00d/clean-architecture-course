<!-- Généré depuis solution.src.md. Modifiez la source, pas ce fichier. -->

# Jour 2 : Solution (TypeScript)

Ce guide reprend pas à pas la construction du domaine en TDD. Il part du projet vide de `jour-2/hexagon/`.

## Le projet de départ

Le domaine vit dans son propre module, qui ne connaît ni l'API, ni la base. C'est le compilateur qui le garantit : ce module ne référence rien.

`jour-2/hexagon/ts` contient un espace de travail npm, avec un paquet `packages/domain` vide. Le fichier `.npmrc` active `install-strategy=linked` : un paquet ne voit que ses propres dépendances, et le domaine n'en déclare aucune. Sans ce réglage, npm rassemblerait toutes les dépendances à la racine, et le domaine pourrait importer une librairie sans l'avoir déclarée.

Installez les dépendances et lancez les tests depuis ce dossier :

```bash
npm install
npm test
```

Aucun test n'est trouvé pour l'instant : c'est normal, et la commande se termine sans erreur. Toutes les commandes de ce guide se lancent depuis `jour-2/hexagon/ts`, et les chemins partent de `packages/domain`.

Vitest exécute le code sans vérifier les types. Lancez `npm run typecheck` pour les vérifier : à l'étape Rouge, c'est lui qui signale ce qui n'existe pas encore.

---

## Les règles du compte

On avance un test à la fois. Ici, plus besoin de configuration, de données à nettoyer ou de compte réservé : un test crée un compte en mémoire, et c'est tout.

### Un dépôt augmente le solde

Le montant et l'identifiant du compte ne sont pas de simples nombres : ce sont des **value objects**. Ils sont définis par leur valeur, sans identité propre : deux montants de 100 sont le même montant, alors que deux comptes au même solde restent deux comptes. On ne confond plus un montant avec un identifiant, et le calcul sur les montants a sa place.

**Rouge**

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

**Vert**

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

### Un retrait diminue le solde

**Rouge**

Ajoutez ce test à `account.test.ts`. Il échoue : `withdraw` n'existe pas encore.

```typescript
test("a withdrawal decreases the balance", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(100), date);

  account.withdraw(new Amount(30), date);

  expect(account.balance).toEqual(new Amount(70));
});
```

**Vert**

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

### Un retrait supérieur au solde est refusé

**Rouge**

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

**Vert**

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

### Un retrait égal au solde est accepté

Ce cas est déjà couvert par le cycle précédent : le test passera du premier coup, et il ne pilote aucun code. On sort donc du TDD le temps d'un test, pour fixer ce comportement à la limite.

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

### Le relevé

**Rouge**

C'est l'exemple du premier jour, avec ses dates. Hier, aucun test ne pouvait le reproduire : l'heure venait du service. Ici, la date fait partie des paramètres.

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

**Vert**

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

### Le relevé trie les opérations par date

**Rouge**

Hier, ce tri n'était pas vraiment testé. Il suffit ici de créer un compte dont les opérations ne sont pas dans l'ordre.

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

**Vert**

Dans `statement`, parcourez les opérations triées :

```typescript
    for (const operation of this.operations.toSorted((left, right) => left.date.getTime() - right.date.getTime())) {
```

Les 6 tests passent.

### Le compte distingue son historique de ses nouvelles opérations

**Rouge**

L'historique d'un compte ne change jamais : une opération enregistrée le reste. Seules les opérations ajoutées depuis le chargement du compte sont à enregistrer, et c'est au compte de les connaître.

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

**Vert**

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

---

## Le service du compte

Les règles sont en place. Il reste à relier le domaine à l'extérieur : ce dont il a besoin, décrit par les ports sortants, et ce qu'il propose, décrit par les ports entrants. Un service implémente les ports entrants à l'aide des ports sortants.

### Les ports sortants et leurs adapters en mémoire

Le domaine décrit ce dont il a besoin, avec son propre vocabulaire. Pour les tests, on écrit nous-mêmes des adapters en mémoire, sans framework de mock.

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

### Les ports entrants

Les ports entrants décrivent ce que l'application propose au monde extérieur, avec un port par fonctionnalité. Pour les nommer, on reprend la convention d'Alistair Cockburn : "for" suivi de l'intention, soit "pour déposer", "pour retirer" et "pour imprimer un relevé".

Pour l'instant, seuls les tests les appellent : on construit un hexagone neuf, et aucun autre adapter entrant n'existe. Les tests sont un adapter comme les autres : ils pilotent l'application par ces ports, et ne connaissent le service qu'au moment de le créer. Le contrôleur et l'écran du DAB les rejoindront le troisième jour.

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

### Déposer

**Rouge**

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

**Vert**

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

### Retirer

**Rouge**

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

**Vert**

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

### Consulter le relevé

**Rouge**

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

**Vert**

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

---

## Comparer avec hier

Arrêtez la base, puis lancez les tests :

```bash
# Depuis la racine du dépôt
supabase stop

# Depuis jour-2/hexagon/ts
npm test
```

Les 10 tests passent, sans base, sans configuration et sans rien à nettoyer. Redémarrez la base avec `supabase start` pour la suite.

Le domaine n'est pas encore branché : l'API utilise toujours le service d'hier. C'est le travail du troisième jour.
