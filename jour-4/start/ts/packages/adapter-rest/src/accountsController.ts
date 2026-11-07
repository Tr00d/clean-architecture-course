import { Router } from "express";
import type { Request, Response } from "express";
import { AccountId, Amount, WithdrawalRefusedError } from "@bank/domain";
import type { ForDepositing, ForPrintingStatements, ForWithdrawing } from "@bank/domain";
import type { AmountRequest, StatementLineResponse } from "./models.ts";

export function accountsController(
  depositing: ForDepositing,
  withdrawing: ForWithdrawing,
  printingStatements: ForPrintingStatements
): Router {
  const router = Router();

  router.param("accountId", (_request, response, next, accountId) => {
    if (Number.isInteger(Number(accountId))) {
      next();
    } else {
      response.sendStatus(404);
    }
  });

  router.get("/accounts/:accountId/operations", async (request: Request, response: Response<StatementLineResponse[]>) => {
    const lines = await printingStatements.printStatement(new AccountId(Number(request.params.accountId)));

    response.json(lines.map(({ date, amount, balance }) => ({ date, amount: amount.value, balance: balance.value })));
  });

  router.post("/accounts/:accountId/deposits", async (request: Request, response: Response) => {
    const { amount } = request.body as AmountRequest;

    await depositing.deposit(new AccountId(Number(request.params.accountId)), new Amount(amount));
    response.sendStatus(201);
  });

  router.post("/accounts/:accountId/withdrawals", async (request: Request, response: Response) => {
    const { amount } = request.body as AmountRequest;

    try {
      await withdrawing.withdraw(new AccountId(Number(request.params.accountId)), new Amount(amount));
      response.sendStatus(201);
    } catch (error) {
      if (error instanceof WithdrawalRefusedError) {
        response.status(422).send(error.message);
      } else {
        throw error;
      }
    }
  });

  return router;
}
