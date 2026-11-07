import type { Account } from "../models/account.ts";
import type { AccountId } from "../models/accountId.ts";

export interface Operations {
  accountOf(accountId: AccountId): Promise<Account>;

  save(account: Account): Promise<void>;
}
