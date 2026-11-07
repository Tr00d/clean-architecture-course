import request from "supertest";
import { expect, test } from "vitest";
import { createApp } from "../src/app.ts";

test("the statement returns the seed operations, newest first, with the running balance", async () => {
  const response = await request(createApp()).get("/accounts/1/operations");

  expect(response.status).toBe(200);
  expect(response.body).toEqual([
    { date: "2021-01-20T12:00:00.000Z", amount: -500, balance: 2500 },
    { date: "2021-01-15T12:00:00.000Z", amount: 2000, balance: 3000 },
    { date: "2021-01-10T12:00:00.000Z", amount: 1000, balance: 1000 }
  ]);
});
