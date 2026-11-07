export class Amount {
  static readonly zero = new Amount(0);

  readonly value: number;

  constructor(value: number) {
    this.value = value;
  }

  plus(other: Amount): Amount {
    return new Amount(this.value + other.value);
  }

  negated(): Amount {
    return new Amount(-this.value);
  }

  isGreaterThan(other: Amount): boolean {
    return this.value > other.value;
  }

  isLessThan(other: Amount): boolean {
    return this.value < other.value;
  }
}
