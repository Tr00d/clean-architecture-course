import { OperationsRepository } from "../data/operationsRepository.ts";
import { InsufficientFundsError } from "./insufficientFundsError.ts";
import type { StatementLine } from "./statementLine.ts";

export class AccountService {
  private readonly repository: OperationsRepository;

  constructor(configuration: NodeJS.ProcessEnv) {
    this.repository = new OperationsRepository(configuration.SUPABASE_URL!, configuration.SUPABASE_KEY!);
  }

  async getStatement(accountId: number): Promise<StatementLine[]> {
    const lines: StatementLine[] = [];
    let balance = 0;

    for (const operation of await this.repository.getOperations(accountId)) {
      balance += operation.amount;
      lines.push({ date: new Date(operation.date), amount: operation.amount, balance });
    }

    return lines.reverse();
  }

  async deposit(accountId: number, amount: number): Promise<void> {
    await this.repository.insertOperation(accountId, amount, new Date());
  }

  async withdraw(accountId: number, amount: number): Promise<void> {
    const operations = await this.repository.getOperations(accountId);
    const balance = operations.reduce((sum, operation) => sum + operation.amount, 0);

    if (amount > balance) {
      throw new InsufficientFundsError(balance);
    }

    await this.repository.insertOperation(accountId, -amount, new Date());
  }
}
