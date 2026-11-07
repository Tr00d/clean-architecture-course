import { Account } from "../src/models/account.ts";
import type { AccountId } from "../src/models/accountId.ts";
import type { Operation } from "../src/models/operation.ts";
import type { Operations } from "../src/ports/operations.ts";

export class InMemoryOperations implements Operations {
  private readonly operations = new Map<number, readonly Operation[]>();

  async accountOf(accountId: AccountId): Promise<Account> {
    return new Account(accountId, this.operations.get(accountId.value) ?? []);
  }

  async save(account: Account): Promise<void> {
    const operations = this.operations.get(account.id.value) ?? [];

    this.operations.set(account.id.value, [...operations, ...account.newOperations]);
  }
}
