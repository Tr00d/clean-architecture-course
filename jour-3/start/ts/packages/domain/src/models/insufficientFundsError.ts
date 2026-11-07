import type { Amount } from "./amount.ts";

export class InsufficientFundsError extends Error {
  constructor(balance: Amount) {
    super(`Insufficient funds: the balance is ${balance.value}.`);
  }
}
