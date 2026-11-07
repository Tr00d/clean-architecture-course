import express from "express";
import type { Express } from "express";
import { SupabaseOperations } from "@bank/adapter-supabase";
import { SystemClock } from "@bank/adapter-time";
import { AccountService } from "@bank/domain";
import { accountsController } from "./accountsController.ts";

export function createApp(): Express {
  const operations = new SupabaseOperations(process.env.SUPABASE_URL!, process.env.SUPABASE_KEY!);
  const service = new AccountService(operations, new SystemClock());
  const app = express();

  app.use(express.json());
  app.use(accountsController(service, service, service));

  return app;
}
