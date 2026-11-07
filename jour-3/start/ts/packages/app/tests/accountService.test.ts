import { createClient } from "@supabase/supabase-js";
import { afterEach, beforeEach, expect, test } from "vitest";
import { AccountService } from "../src/business/accountService.ts";
import { InsufficientFundsError } from "../src/business/insufficientFundsError.ts";

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

test("a withdrawal is refused when above the balance", async () => {
  const service = new AccountService(process.env);
  await service.deposit(accountId, 100);

  await expect(service.withdraw(accountId, 100.01)).rejects.toThrow(InsufficientFundsError);

  const statement = await service.getStatement(accountId);
  expect(statement).toHaveLength(1);
  expect(statement[0].balance).toBe(100);
});

test("a withdrawal is accepted when equal to the balance", async () => {
  const service = new AccountService(process.env);
  await service.deposit(accountId, 100);

  await service.withdraw(accountId, 100);

  const statement = await service.getStatement(accountId);
  expect(statement[0].balance).toBe(0);
});
