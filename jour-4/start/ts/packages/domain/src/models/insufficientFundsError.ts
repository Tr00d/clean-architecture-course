import { WithdrawalRefusedError } from "./withdrawalRefusedError.ts";
import type { Amount } from "./amount.ts";

export class InsufficientFundsError extends WithdrawalRefusedError {
  constructor(balance: Amount) {
    super(`Insufficient funds: the balance is ${balance.value}.`);
  }
}
