import type { Amount } from "./amount.ts";

export interface StatementLine {
  date: Date;
  amount: Amount;
  balance: Amount;
}
