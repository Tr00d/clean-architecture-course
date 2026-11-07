import { WithdrawalRefusedError } from "./withdrawalRefusedError.ts";
import type { Amount } from "./amount.ts";

export class NightWithdrawalLimitExceededError extends WithdrawalRefusedError {
  constructor(limit: Amount) {
    super(`Night withdrawal limit exceeded: at most ${limit.value} can be withdrawn between 22:00 and 06:00.`);
  }
}
