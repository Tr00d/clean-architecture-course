import { createInterface } from "node:readline";
import { SupabaseOperations } from "@bank/adapter-supabase";
import { SystemClock } from "@bank/adapter-time";
import { AccountId, AccountService } from "@bank/domain";
import { AtmScreen } from "./atmScreen.ts";

const operations = new SupabaseOperations(process.env.SUPABASE_URL!, process.env.SUPABASE_KEY!);
const service = new AccountService(operations, new SystemClock());

const screen = new AtmScreen(
  new AccountId(Number(process.argv[2])),
  service,
  service,
  service,
  createInterface({ input: process.stdin }),
  console.log
);

console.log("Commands: deposit <amount>, withdraw <amount>, statement, quit");
await screen.run();
