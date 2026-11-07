import type { AccountId } from "../models/accountId.ts";
import type { Amount } from "../models/amount.ts";

export interface ForDepositing {
  deposit(accountId: AccountId, amount: Amount): Promise<void>;
}
