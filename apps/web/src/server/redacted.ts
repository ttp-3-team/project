/** A secret that never prints itself. Unwrap only where the raw value is needed. */
export class Redacted {
  readonly #value: string

  constructor(value: string) {
    this.#value = value
  }

  unwrap(): string {
    return this.#value
  }

  toString(): string {
    return '<redacted>'
  }

  toJSON(): string {
    return '<redacted>'
  }
}
