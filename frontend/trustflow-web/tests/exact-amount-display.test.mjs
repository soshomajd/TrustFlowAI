import assert from "node:assert/strict";
import { registerHooks } from "node:module";
import { test } from "node:test";

registerHooks({
  resolve(specifier, context, nextResolve) {
    return nextResolve(specifier.startsWith("@/")
      ? new URL(`../src/${specifier.slice(2)}.ts`, import.meta.url).href
      : specifier, context);
  },
});

const { moneyText, formatMoney, formatMoneyCurrency, sumMoney, subtractMoney, moneyUnits, moneyShare } =
  await import("../src/lib/money/exact-money.ts");
const { getSettlementBaseUnits } = await import("../src/features/review/lib/settlement-amount.ts");
const { createMilestoneSchema } = await import("../src/features/milestones/schemas/create-milestone-schema.ts");

test("large REST decimals keep their original cents instead of the parsed JSON number", () => {
  const dto = JSON.parse('{"budget":9007199254740991.01,"budgetExact":"9007199254740991.01"}');
  assert.notEqual(String(dto.budget), dto.budgetExact);
  assert.equal(formatMoney(moneyText(dto, "budget")), "9,007,199,254,740,991.01");
});

for (const [value, expected] of [
  ["0.000001", "0.000001"],
  ["79228162514264337593543950335", "79,228,162,514,264,337,593,543,950,335"],
  ["0.0000000000000000000000000001", "0.0000000000000000000000000001"],
  ["100.000000", "100"],
  ["-0.010000", "-0.01"],
  ["-1234.010001", "-1,234.010001"],
  [0.000001, "0.000001"],
]) test(`exact formatting of ${value}`, () => assert.equal(formatMoney(value), expected));

test("currency display keeps sub-cent refunds and net awards", () => {
  assert.equal(formatMoneyCurrency("299.997"), "$299.997");
  assert.equal(formatMoneyCurrency("100"), "$100.00");
  assert.equal(formatMoneyCurrency("-0.01"), "-$0.01");
});

test("project and proposal totals and a one-cent mismatch stay exact at large magnitudes", () => {
  const total = sumMoney(["9007199254740990.99", "0.02"]);
  assert.equal(total, "9007199254740991.01");
  assert.equal(subtractMoney(total, "9007199254740991"), "0.01");
  assert.notEqual(moneyUnits(total), moneyUnits("9007199254740991"));
  assert.equal(sumMoney(["0.1", "0.2"]), "0.3");
});

test("recorded gross, fee and net remain separate exact values", () => {
  const dto = JSON.parse('{"releasedAmount":9007199254740991.01,"releasedAmountExact":"9007199254740991.01","platformFeesPaid":900719925474099.101,"platformFeesPaidExact":"900719925474099.101","freelancerNetPaid":8106479329266891.909,"freelancerNetPaidExact":"8106479329266891.909"}');
  assert.equal(sumMoney([moneyText(dto, "platformFeesPaid"), moneyText(dto, "freelancerNetPaid")]), moneyText(dto, "releasedAmount"));
  assert.equal(formatMoney(moneyText(dto, "freelancerNetPaid")), "8,106,479,329,266,891.909");
});

test("reviewer exact display can be checked against chain even when the numeric compatibility field is rounded", () => {
  const dto = JSON.parse('{"milestoneAmount":9007199254740991.01,"milestoneAmountExact":"9007199254740991.01"}');
  assert.equal(getSettlementBaseUnits("9007199254740991.01", moneyText(dto, "milestoneAmount"), 6, 9007199254740991010000n), 9007199254740991010000n);
  assert.throws(() => getSettlementBaseUnits("9007199254740991.02", moneyText(dto, "milestoneAmount"), 6, 9007199254740991010000n), /cannot exceed/);
});

test("half and AI percentage previews floor to cents without changing large amounts", () => {
  assert.equal(moneyShare("9007199254740991.01", 50), "4503599627370495.5");
  assert.equal(moneyShare("333.33", 33), "109.99");
  assert.equal(subtractMoney("9007199254740991.01", "4503599627370495.5"), "4503599627370495.51");
});

test("ordinary legacy responses and nullable payout fields still work", () => {
  assert.equal(moneyText({ amount: 12.34 }, "amount"), "12.34");
  assert.equal(moneyText({ paidAmount: null, paidAmountExact: null }, "paidAmount"), "0");
});

test("invalid and unsupported precision are rejected, never rounded", () => {
  for (const value of [NaN, Infinity, Number.MAX_SAFE_INTEGER + 1, "1e20", "1,000", "0.00000000000000000000000000001"])
    assert.throws(() => formatMoney(value));
});

test("remaining milestone budget comparison uses exact text even within numeric request limits", () => {
  const schema = createMilestoneSchema({ projectDeadline: "2099-12-31", remainingBudget: "35184372088832.01", existingSequenceNumbers: [] });
  const stage = { title: "Exact stage", description: "Check the remaining budget limit", sequenceNumber: "1", deadline: "2099-01-01" };
  assert.equal(schema.safeParse({ ...stage, amount: "35184372088832.01" }).success, true);
  assert.equal(schema.safeParse({ ...stage, amount: "35184372088832.02" }).success, false);
});
