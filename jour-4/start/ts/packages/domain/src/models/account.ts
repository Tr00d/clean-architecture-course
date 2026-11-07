import { Amount } from "./amount.ts";
import { InsufficientFundsError } from "./insufficientFundsError.ts";
import { NightWithdrawalLimitExceededError } from "./nightWithdrawalLimitExceededError.ts";
import { WithdrawalLimitExceededError } from "./withdrawalLimitExceededError.ts";
import type { AccountId } from "./accountId.ts";
import type { Operation } from "./operation.ts";
import type { StatementLine } from "./statementLine.ts";

const withdrawalLimit = new Amount(2500);
const dayInMilliseconds = 24 * 60 * 60 * 1000;
const nightWithdrawalLimit = new Amount(500);

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

    if (this.withdrawnInThe24HoursBefore(date).plus(amount).isGreaterThan(withdrawalLimit)) {
      throw new WithdrawalLimitExceededError(withdrawalLimit);
    }

    if (this.isNight(date) && amount.isGreaterThan(nightWithdrawalLimit)) {
      throw new NightWithdrawalLimitExceededError(nightWithdrawalLimit);
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

  private withdrawnInThe24HoursBefore(date: Date): Amount {
    return this.allOperations
      .filter((operation) => operation.amount.isLessThan(Amount.zero))
      .filter((operation) => operation.date.getTime() > date.getTime() - dayInMilliseconds)
      .reduce((total, operation) => total.plus(operation.amount.negated()), Amount.zero);
  }

  private isNight(date: Date): boolean {
    const hour = date.getUTCHours();

    return hour >= 22 || hour < 6;
  }
}
