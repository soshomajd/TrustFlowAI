import assert from "node:assert/strict";
import { registerHooks } from "node:module";
import { test } from "node:test";
import { formatUnits } from "viem";
import { decimalToBaseUnits } from "../src/lib/money/decimal-amount.ts";

registerHooks({
  resolve(specifier, context, nextResolve) {
    return nextResolve(specifier.startsWith("@/")
      ? new URL(`../src/${specifier.slice(2)}.ts`, import.meta.url).href
      : specifier, context);
  },
});
const { previewPlatformFee } = await import("../src/features/escrow/lib/platform-fee.ts");

for (const [gross, fee, net] of [
  ["0", "0", "0"],
  ["0.000001", "0", "0.000001"],
  ["0.000009", "0", "0.000009"],
  ["0.000010", "0.000001", "0.000009"],
  ["0.000011", "0.000001", "0.00001"],
  ["0.000019", "0.000001", "0.000018"],
  ["0.000101", "0.00001", "0.000091"],
  ["0.01", "0.001", "0.009"],
  ["1.01", "0.101", "0.909"],
  ["333.33", "33.333", "299.997"],
  ["90071992547409.91", "9007199254740.991", "81064793292668.919"],
]) {
  test(`six-decimal gross ${gross} preserves the exact fee and freelancer net`, () => {
    assert.deepEqual(previewPlatformFee(gross, 6, 1000), { fee, net });
  });
}

test("all supported token precisions conserve each payment and floor to less than one base unit", () => {
  for (let decimals = 0; decimals <= 18; decimals++) {
    for (const gross of [0n, 1n, 9n, 10n, 11n, 19n, 101n, 9999999999999999999999999999999999n, (1n << 256n) - 1n]) {
      const preview = previewPlatformFee(formatUnits(gross, decimals), decimals, 1000);
      const fee = decimalToBaseUnits(preview.fee, decimals);
      const net = decimalToBaseUnits(preview.net, decimals);
      assert.equal(fee + net, gross);
      assert.ok(fee * 10n <= gross);
      assert.ok((fee + 1n) * 10n > gross);
    }
  }
});

test("small payouts never charge extra to recover fractions from another payout", () => {
  const payments = [101n, 19n];
  const fees = payments.map(gross => decimalToBaseUnits(previewPlatformFee(formatUnits(gross, 6), 6, 1000).fee, 6));
  assert.equal(fees[0] + fees[1], 11n);
  assert.equal(decimalToBaseUnits(previewPlatformFee("0.000120", 6, 1000).fee, 6), 12n);
});

test("legacy escrows keep the full gross amount without a fee", () => {
  for (const gross of ["0", "0.000001", "333.33"]) {
    assert.deepEqual(previewPlatformFee(gross, 6, 0), { fee: "0", net: gross });
  }
});

test("invalid policy and inexact input are refused instead of previewing a different payment", () => {
  assert.throws(() => previewPlatformFee("1", 6, 999), /Unsupported fee policy/);
  for (const gross of ["-1", "0.0000009", "1.0000001"]) {
    assert.throws(() => previewPlatformFee(gross, 6, 1000));
  }
});
