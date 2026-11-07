export class InsufficientFundsError extends Error {
  constructor(balance: number) {
    super(`Insufficient funds: the balance is ${balance}.`);
  }
}
