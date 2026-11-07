import { createClient } from "@supabase/supabase-js";
import { afterEach, beforeEach, expect, test } from "vitest";
import { SupabaseOperations } from "@bank/adapter-supabase";
import { SystemClock } from "@bank/adapter-time";
import { AccountId, AccountService, Amount, InsufficientFundsError } from "@bank/domain";

const accountId = new AccountId(42);

function createService(): AccountService {
  const operations = new SupabaseOperations(process.env.SUPABASE_URL!, process.env.SUPABASE_KEY!);

  return new AccountService(operations, new SystemClock());
}

async function clearOperations(): Promise<void> {
  const client = createClient(process.env.SUPABASE_URL!, process.env.SUPABASE_KEY!);

  const { error } = await client.from("operations").delete().eq("account_id", accountId.value);

  if (error) {
    throw error;
  }
}

beforeEach(clearOperations);
afterEach(clearOperations);

test("the statement is empty when the account has no operations", async () => {
  const service = createService();

  const statement = await service.printStatement(accountId);

  expect(statement).toEqual([]);
});

test("the statement lists the operations, newest first, with the running balance", async () => {
  const service = createService();
  await service.deposit(accountId, new Amount(1000));
  await service.deposit(accountId, new Amount(2000));
  await service.withdraw(accountId, new Amount(500));

  const statement = await service.printStatement(accountId);

  expect(statement.map((line) => line.amount.value)).toEqual([-500, 2000, 1000]);
  expect(statement.map((line) => line.balance.value)).toEqual([2500, 3000, 1000]);
});

test("the statement dates the operations with the current time", async () => {
  const service = createService();
  const before = Date.now();

  await service.deposit(accountId, new Amount(1000));

  const [line] = await service.printStatement(accountId);
  expect(line.date.getTime()).toBeGreaterThanOrEqual(before - 1000);
  expect(line.date.getTime()).toBeLessThanOrEqual(Date.now());
});

test("a withdrawal is refused when above the balance", async () => {
  const service = createService();
  await service.deposit(accountId, new Amount(100));

  await expect(service.withdraw(accountId, new Amount(100.01))).rejects.toThrow(InsufficientFundsError);

  const statement = await service.printStatement(accountId);
  expect(statement).toHaveLength(1);
  expect(statement[0].balance).toEqual(new Amount(100));
});

test("a withdrawal is accepted when equal to the balance", async () => {
  const service = createService();
  await service.deposit(accountId, new Amount(100));

  await service.withdraw(accountId, new Amount(100));

  const statement = await service.printStatement(accountId);
  expect(statement[0].balance).toEqual(Amount.zero);
});
