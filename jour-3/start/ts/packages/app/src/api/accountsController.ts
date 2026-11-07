import { Router } from "express";
import type { Request, Response } from "express";
import type { AccountService } from "../business/accountService.ts";
import { InsufficientFundsError } from "../business/insufficientFundsError.ts";
import type { AmountRequest, StatementLineResponse } from "./models.ts";

export function accountsController(accountService: AccountService): Router {
  const router = Router();

  router.param("accountId", (_request, response, next, accountId) => {
    if (Number.isInteger(Number(accountId))) {
      next();
    } else {
      response.sendStatus(404);
    }
  });

  router.get("/accounts/:accountId/operations", async (request: Request, response: Response<StatementLineResponse[]>) => {
    const lines = await accountService.getStatement(Number(request.params.accountId));

    response.json(lines.map(({ date, amount, balance }) => ({ date, amount, balance })));
  });

  router.post("/accounts/:accountId/deposits", async (request: Request, response: Response) => {
    const { amount } = request.body as AmountRequest;

    await accountService.deposit(Number(request.params.accountId), amount);
    response.sendStatus(201);
  });

  router.post("/accounts/:accountId/withdrawals", async (request: Request, response: Response) => {
    const { amount } = request.body as AmountRequest;

    try {
      await accountService.withdraw(Number(request.params.accountId), amount);
      response.sendStatus(201);
    } catch (error) {
      if (error instanceof InsufficientFundsError) {
        response.status(422).send(error.message);
      } else {
        throw error;
      }
    }
  });

  return router;
}
