export interface AmountRequest {
  amount: number;
}

export interface StatementLineResponse {
  date: Date;
  amount: number;
  balance: number;
}
