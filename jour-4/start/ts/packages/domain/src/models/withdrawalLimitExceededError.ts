import { WithdrawalRefusedError } from "./withdrawalRefusedError.ts";
import type { Amount } from "./amount.ts";

export class WithdrawalLimitExceededError extends WithdrawalRefusedError {
  constructor(limit: Amount) {
    super(`Withdrawal limit exceeded: at most ${limit.value} can be withdrawn over 24 hours.`);
  }
}
