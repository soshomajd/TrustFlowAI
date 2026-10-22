import assert from "node:assert/strict";
import { registerHooks } from "node:module";
import { test } from "node:test";

// Node's TS stripping does not resolve Next's @/ aliases. Resolve those here
// so the tests exercise the production Zod schemas without a second compiler.
registerHooks({
  resolve(specifier, context, nextResolve) {
    return nextResolve(specifier.startsWith("@/")
      ? new URL(`../src/${specifier.slice(2)}.ts`, import.meta.url).href
      : specifier, context);
  },
});

const { createProjectSchema } = await import("../src/features/projects/schemas/create-project-schema.ts");
const { createMilestoneSchema } = await import("../src/features/milestones/schemas/create-milestone-schema.ts");
const { getSettlementBaseUnits } = await import("../src/features/review/lib/settlement-amount.ts");
const deadline = "2099-01-01";
const project = { title: "Exact budget test", description: "Check the original cents survive the API request.", deadline };
const milestoneSchema = createMilestoneSchema({ projectDeadline: "2099-12-31T23:59:59Z", remainingBudget: 90071992547409.9, existingSequenceNumbers: [] });
const milestone = { title: "Exact stage test", description: "No automatic rounding of the amount.", deadline, sequenceNumber: "1" };

test("reviewer sends the exact original text instead of a rounded binary number", () => {
  assert.equal(getSettlementBaseUnits("35184372088832.01", 35184372088832.01, 6, 35184372088832010000n), 35184372088832010000n);
});

test("a case whose JSON number lost cents cannot reach a settlement signature", () => {
  assert.throws(() => getSettlementBaseUnits("90071992547409.90", Number("90071992547409.91"), 6, 90071992547409910000n), /displayed case amount does not match/);
});

test("settlement limits use the exact on-chain amount", () => {
  assert.throws(() => getSettlementBaseUnits("100.01", 100, 6, 100000000n), /cannot exceed/);
  assert.throws(() => getSettlementBaseUnits("-0.01", 100, 6, 100000000n), /non-negative/);
});

for (const budget of ["0.01", "35184372088832.01", "90071992547409.89"]) {
  test(`project and stage preserve ${budget} in their actual JSON requests`, () => {
    const created = createProjectSchema.parse({ ...project, budget });
    const stage = milestoneSchema.parse({ ...milestone, amount: budget });
    assert.equal(JSON.parse(JSON.stringify(created)).budget.toString(), budget);
    assert.equal(JSON.parse(JSON.stringify(stage)).amount.toString(), budget);
  });
}

for (const budget of ["90071992547409.91", "9999999999999999.99", "0.001"]) {
  test(`project and stage reject ${budget} instead of changing it`, () => {
    assert.equal(createProjectSchema.safeParse({ ...project, budget }).success, false);
    assert.equal(milestoneSchema.safeParse({ ...milestone, amount: budget }).success, false);
  });
}
