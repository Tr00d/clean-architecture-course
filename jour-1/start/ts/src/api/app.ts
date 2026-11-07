import express from "express";
import type { Express } from "express";
import { AccountService } from "../business/accountService.ts";
import { accountsController } from "./accountsController.ts";

export function createApp(): Express {
  const app = express();

  app.use(express.json());
  app.use(accountsController(new AccountService(process.env)));

  return app;
}
