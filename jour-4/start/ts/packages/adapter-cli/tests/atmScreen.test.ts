import { beforeEach, expect, test } from "vitest";
import { AccountId, AccountService, Amount } from "@bank/domain";
import { FixedClock } from "../../domain/tests/fixedClock.ts";
import { InMemoryOperations } from "../../domain/tests/inMemoryOperations.ts";
import { AtmScreen } from "../src/atmScreen.ts";

const id = new AccountId(1);
const now = new Date("2021-01-10T00:00:00Z");

let operations: InMemoryOperations;

beforeEach(() => {
  operations = new InMemoryOperations();
});

async function run(...commands: string[]): Promise<string[]> {
  const output: string[] = [];
  const service = new AccountService(operations, new FixedClock(now));
  const screen = new AtmScreen(id, service, service, service, commands, (line) => output.push(line));

  await screen.run();

  return output;
}

test("a deposit increases the balance", async () => {
  await run("deposit 100");

  const account = await operations.accountOf(id);
  expect(account.balance).toEqual(new Amount(100));
});

test("a withdrawal decreases the balance", async () => {
  await run("deposit 100", "withdraw 30");

  const account = await operations.accountOf(id);
  expect(account.balance).toEqual(new Amount(70));
});

test("a refused withdrawal displays the reason", async () => {
  const output = await run("withdraw 30");

  expect(output.join("\n")).toContain("Insufficient funds");
});

test("the statement displays the operations and the balance", async () => {
  const output = await run("deposit 100", "statement");

  expect(output).toEqual(["2021-01-10 | 100 | 100", "Balance: 100"]);
});
