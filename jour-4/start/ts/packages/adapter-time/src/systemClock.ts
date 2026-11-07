import type { Clock } from "@bank/domain";

export class SystemClock implements Clock {
  now(): Date {
    return new Date();
  }
}
