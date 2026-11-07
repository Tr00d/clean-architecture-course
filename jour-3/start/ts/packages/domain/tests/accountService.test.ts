import { beforeEach, expect, test } from "vitest";
import { AccountId } from "../src/models/accountId.ts";
import { Amount } from "../src/models/amount.ts";
import { AccountService } from "../src/services/accountService.ts";
import { FixedClock } from "./fixedClock.ts";
import { InMemoryOperations } from "./inMemoryOperations.ts";
import type { ForDepositing } from "../src/ports/forDepositing.ts";
import type { ForPrintingStatements } from "../src/ports/forPrintingStatements.ts";
import type { ForWithdrawing } from "../src/ports/forWithdrawing.ts";

const id = new AccountId(1);
const now = new Date("2021-01-10T00:00:00Z");

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

test("a deposit saves the operation at the current date", async () => {
  await depositing.deposit(id, new Amount(100));

  const account = await operations.accountOf(id);
  expect(account.history).toEqual([{ amount: new Amount(100), date: now }]);
});

test("a withdrawal saves the operation at the current date", async () => {
  await depositing.deposit(id, new Amount(100));

  await withdrawing.withdraw(id, new Amount(30));

  const account = await operations.accountOf(id);
  expect(account.history.at(-1)).toEqual({ amount: new Amount(-30), date: now });
});

test("printing a statement returns the statement of the account", async () => {
  await depositing.deposit(id, new Amount(100));

  const statement = await printingStatements.printStatement(id);

  expect(statement).toEqual([{ date: now, amount: new Amount(100), balance: new Amount(100) }]);
});
