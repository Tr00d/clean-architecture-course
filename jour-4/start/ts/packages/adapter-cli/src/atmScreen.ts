import { Amount, WithdrawalRefusedError } from "@bank/domain";
import type { AccountId, ForDepositing, ForPrintingStatements, ForWithdrawing } from "@bank/domain";

function parseAmount(value: string): Amount {
  return new Amount(Number(value));
}

export class AtmScreen {
  private readonly accountId: AccountId;
  private readonly depositing: ForDepositing;
  private readonly withdrawing: ForWithdrawing;
  private readonly printingStatements: ForPrintingStatements;
  private readonly input: AsyncIterable<string> | Iterable<string>;
  private readonly output: (line: string) => void;

  constructor(
    accountId: AccountId,
    depositing: ForDepositing,
    withdrawing: ForWithdrawing,
    printingStatements: ForPrintingStatements,
    input: AsyncIterable<string> | Iterable<string>,
    output: (line: string) => void
  ) {
    this.accountId = accountId;
    this.depositing = depositing;
    this.withdrawing = withdrawing;
    this.printingStatements = printingStatements;
    this.input = input;
    this.output = output;
  }

  async run(): Promise<void> {
    for await (const line of this.input) {
      if (line === "quit") {
        break;
      }

      const [command, value] = line.split(" ");

      switch (command) {
        case "deposit":
          await this.depositing.deposit(this.accountId, parseAmount(value));
          break;
        case "withdraw":
          await this.withdraw(parseAmount(value));
          break;
        case "statement":
          await this.printStatement();
          break;
      }
    }
  }

  private async withdraw(amount: Amount): Promise<void> {
    try {
      await this.withdrawing.withdraw(this.accountId, amount);
    } catch (error) {
      if (error instanceof WithdrawalRefusedError) {
        this.output(error.message);
      } else {
        throw error;
      }
    }
  }

  private async printStatement(): Promise<void> {
    const statement = await this.printingStatements.printStatement(this.accountId);

    for (const line of statement) {
      this.output(`${line.date.toISOString().slice(0, 10)} | ${line.amount.value} | ${line.balance.value}`);
    }

    this.output(`Balance: ${(statement[0]?.balance ?? Amount.zero).value}`);
  }
}
