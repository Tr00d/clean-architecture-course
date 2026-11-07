import type { Amount } from "./amount.ts";

export interface Operation {
  amount: Amount;
  date: Date;
}
