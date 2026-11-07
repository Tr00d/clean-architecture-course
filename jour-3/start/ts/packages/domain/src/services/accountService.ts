import type { AccountId } from "../models/accountId.ts";
import type { Amount } from "../models/amount.ts";
import type { StatementLine } from "../models/statementLine.ts";
import type { Clock } from "../ports/clock.ts";
import type { ForDepositing } from "../ports/forDepositing.ts";
import type { ForPrintingStatements } from "../ports/forPrintingStatements.ts";
import type { ForWithdrawing } from "../ports/forWithdrawing.ts";
import type { Operations } from "../ports/operations.ts";

export class AccountService implements ForDepositing, ForPrintingStatements, ForWithdrawing {
  private readonly operations: Operations;
  private readonly clock: Clock;

  constructor(operations: Operations, clock: Clock) {
    this.operations = operations;
    this.clock = clock;
  }

  async deposit(accountId: AccountId, amount: Amount): Promise<void> {
    const account = await this.operations.accountOf(accountId);
    account.deposit(amount, this.clock.now());
    await this.operations.save(account);
  }

  async withdraw(accountId: AccountId, amount: Amount): Promise<void> {
    const account = await this.operations.accountOf(accountId);
    account.withdraw(amount, this.clock.now());
    await this.operations.save(account);
  }

  async printStatement(accountId: AccountId): Promise<StatementLine[]> {
    const account = await this.operations.accountOf(accountId);
    return account.statement();
  }
}
