import { expect, test } from "vitest";
import { Account } from "../src/models/account.ts";
import { AccountId } from "../src/models/accountId.ts";
import { Amount } from "../src/models/amount.ts";
import { InsufficientFundsError } from "../src/models/insufficientFundsError.ts";
import { NightWithdrawalLimitExceededError } from "../src/models/nightWithdrawalLimitExceededError.ts";
import { WithdrawalLimitExceededError } from "../src/models/withdrawalLimitExceededError.ts";

function hoursAfter(start: Date, hours: number): Date {
  return new Date(start.getTime() + hours * 60 * 60 * 1000);
}

const id = new AccountId(1);
const date = new Date("2021-01-10T12:00:00Z");

test("a deposit increases the balance", () => {
  const account = new Account(id, []);

  account.deposit(new Amount(100), date);

  expect(account.balance).toEqual(new Amount(100));
});

test("a withdrawal decreases the balance", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(100), date);

  account.withdraw(new Amount(30), date);

  expect(account.balance).toEqual(new Amount(70));
});

test("a withdrawal is refused when above the balance", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(100), date);

  expect(() => account.withdraw(new Amount(100.01), date)).toThrow(InsufficientFundsError);

  expect(account.balance).toEqual(new Amount(100));
});

test("a withdrawal is accepted when equal to the balance", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(100), date);

  account.withdraw(new Amount(100), date);

  expect(account.balance).toEqual(Amount.zero);
});

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

test("the statement sorts the operations by date, whatever their order", () => {
  const account = new Account(id, [
    { amount: new Amount(-50), date: new Date("2021-02-02T00:00:00Z") },
    { amount: new Amount(200), date: new Date("2021-02-01T00:00:00Z") }
  ]);

  const statement = account.statement();

  expect(statement.map((line) => line.balance)).toEqual([new Amount(150), new Amount(200)]);
});

test("the new operations only contain what was added to the history", () => {
  const account = new Account(id, [{ amount: new Amount(100), date }]);

  account.deposit(new Amount(50), date);

  expect(account.newOperations).toEqual([{ amount: new Amount(50), date }]);
});

test("a withdrawal is refused when above the limit", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(5000), date);

  expect(() => account.withdraw(new Amount(2500.01), date)).toThrow(WithdrawalLimitExceededError);

  expect(account.balance).toEqual(new Amount(5000));
});

test("a withdrawal is refused when withdrawals add up above the limit", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(5000), date);
  account.withdraw(new Amount(1500), date);

  expect(() => account.withdraw(new Amount(1500), hoursAfter(date, 1))).toThrow(WithdrawalLimitExceededError);
});

test("a withdrawal is accepted when previous withdrawals are 24 hours old", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(5000), date);
  account.withdraw(new Amount(1500), date);

  account.withdraw(new Amount(1500), hoursAfter(date, 24));

  expect(account.balance).toEqual(new Amount(2000));
});

test("a withdrawal is accepted when equal to the limit", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(5000), date);

  account.withdraw(new Amount(2500), date);

  expect(account.balance).toEqual(new Amount(2500));
});

test("a withdrawal is refused when above the night limit, at nightfall", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(1000), date);
  const nightfall = new Date("2021-01-10T22:00:00Z");

  expect(() => account.withdraw(new Amount(500.01), nightfall)).toThrow(NightWithdrawalLimitExceededError);

  expect(account.balance).toEqual(new Amount(1000));
});

test("a withdrawal is refused when above the night limit, before dawn", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(1000), date);
  const beforeDawn = new Date("2021-01-10T05:59:00Z");

  expect(() => account.withdraw(new Amount(500.01), beforeDawn)).toThrow(NightWithdrawalLimitExceededError);
});

test("a withdrawal is accepted above the night limit, at dawn", () => {
  const account = new Account(id, []);
  account.deposit(new Amount(1000), date);
  const dawn = new Date("2021-01-10T06:00:00Z");

  account.withdraw(new Amount(600), dawn);

  expect(account.balance).toEqual(new Amount(400));
});
