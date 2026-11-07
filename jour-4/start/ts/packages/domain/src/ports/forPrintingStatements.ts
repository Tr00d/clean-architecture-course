import type { AccountId } from "../models/accountId.ts";
import type { StatementLine } from "../models/statementLine.ts";

export interface ForPrintingStatements {
  printStatement(accountId: AccountId): Promise<StatementLine[]>;
}
