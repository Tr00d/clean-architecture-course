import type { Clock } from "../src/ports/clock.ts";

export class FixedClock implements Clock {
  private readonly date: Date;

  constructor(date: Date) {
    this.date = date;
  }

  now(): Date {
    return this.date;
  }
}
