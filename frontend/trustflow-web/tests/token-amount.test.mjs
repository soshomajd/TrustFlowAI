import assert from "node:assert/strict";
import { test } from "node:test";

import { decimalToBaseUnits, formatBaseUnits, isExactJsonMoneyInput } from "../src/lib/money/decimal-amount.ts";

for (const [amount, decimals, expected] of [
  ["90071992547409.91", 6, 90071992547409910000n],
  ["90071992547409.89", 6, 90071992547409890000n],
  ["35184372088832.01", 6, 35184372088832010000n],
  ["9999999999999999.99", 18, 9999999999999999990000000000000000n],
  ["0.000000000000000001", 18, 1n],
  [1e-18, 18, 1n],
  [1e-7, 7, 1n],
  ["1.2300000", 6, 1230000n],
  ["12.000", 0, 12n],
  [0, 6, 0n],
]) {
  test(`${String(amount)} converts exactly at ${decimals} decimals`, () => {
    assert.equal(decimalToBaseUnits(amount, decimals), expected);
  });
}

test("JSON numbers use their decimal representation without binary toFixed artifacts", () => {
  assert.equal(decimalToBaseUnits(35184372088832.01, 6), 35184372088832010000n);
  assert.equal(decimalToBaseUnits(90071992547409.89, 6), 90071992547409890000n);
});

test("unsafe request text is refused instead of silently changing its cents", () => {
  assert.equal(JSON.stringify(Number("90071992547409.91")), "90071992547409.9");
  assert.equal(isExactJsonMoneyInput("90071992547409.91"), false);
  assert.equal(isExactJsonMoneyInput("90071992547409.89"), true);
  assert.equal(isExactJsonMoneyInput("35184372088832.01"), true);
  assert.equal(isExactJsonMoneyInput("0.01"), true);
  assert.equal(isExactJsonMoneyInput("9999999999999999.99"), false);
});

test("extra non-zero fractional digits never round up or down", () => {
  for (const amount of ["0.0000001", "1.0000009", "1.23456789", 0.1 + 0.2]) {
    assert.throws(() => decimalToBaseUnits(amount, 6), /cannot be represented exactly/);
  }
});

test("invalid or ambiguous text and unsafe numeric values never reach the wallet", () => {
  for (const amount of ["", "-1", "+1", "1,234", "1e3", "0x10", "NaN", "Infinity", ".1", Infinity, NaN, -1, Number.MAX_SAFE_INTEGER + 1]) {
    assert.throws(() => decimalToBaseUnits(amount, 6));
  }
  for (const decimals of [-1, 19, 1.5, NaN, Infinity]) {
    assert.throws(() => decimalToBaseUnits("1", decimals), /decimals must be between/);
  }
});

test("uint256 maximum is exact, and an overflowing amount is refused", () => {
  const maximum = (1n << 256n) - 1n;
  assert.equal(decimalToBaseUnits(maximum.toString(), 0), maximum);
  assert.throws(() => decimalToBaseUnits((maximum + 1n).toString(), 0), /too large/);
});

test("funding balance and required total display every base unit without Number conversion", () => {
  assert.equal(formatBaseUnits(90071992547409910000n, 6), "90,071,992,547,409.91");
  assert.equal(formatBaseUnits(1n, 18), "0.000000000000000001");
  assert.equal(formatBaseUnits(1230000n, 6), "1.23");
  assert.equal(formatBaseUnits(123n, 0), "123");
  assert.equal(formatBaseUnits(0n, 18), "0");
});

test("grouped display round-trips through the exact parser at all supported precisions", () => {
  for (let decimals = 0; decimals <= 18; decimals++) {
    for (const units of [0n, 1n, 9999999999999999999999999999999999n]) {
      assert.equal(decimalToBaseUnits(formatBaseUnits(units, decimals).replaceAll(",", ""), decimals), units);
    }
  }
});
