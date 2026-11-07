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
