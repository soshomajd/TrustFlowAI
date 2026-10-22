import { decimalToBaseUnits } from "@/lib/money/decimal-amount";

export type MoneyAmount = string | number;
const SCALE = 28;
const UNIT = 10n ** BigInt(SCALE);

// Decimal response companions come from .NET decimal, before JSON numbers
// can lose precision. Numeric fallback supports older, ordinary responses.
export function moneyText<T extends object>(source: T, field: keyof T): string {
  const values = source as Record<string, unknown>;
  const exact = values[`${String(field)}Exact`];
  const value = typeof exact === "string" ? exact : (values[String(field)] as MoneyAmount | null | undefined) ?? "0";
  return canonicalMoney(value);
}

export function canonicalMoney(value: MoneyAmount): string {
  let text = String(value).trim();
  if (typeof value === "number") {
    const negative = value < 0;
    const units = decimalToBaseUnits(Math.abs(value), 18);
    text = `${negative ? "-" : ""}${units / 10n ** 18n}.${(units % 10n ** 18n).toString().padStart(18, "0")}`;
  }
  if (!/^-?\d+(?:\.\d{1,28})?$/.test(text)) throw new Error("Invalid exact money amount.");
  const negative = text.startsWith("-");
  const [whole, fraction = ""] = text.replace(/^-/, "").split(".");
  const normalizedWhole = BigInt(whole).toString();
  const normalizedFraction = fraction.replace(/0+$/, "");
  const result = normalizedFraction ? `${normalizedWhole}.${normalizedFraction}` : normalizedWhole;
  return negative && result !== "0" ? `-${result}` : result;
}

export function moneyUnits(value: MoneyAmount): bigint {
  const text = canonicalMoney(value);
  const [whole, fraction = ""] = text.replace(/^-/, "").split(".");
  const units = BigInt(whole) * UNIT + BigInt(fraction.padEnd(SCALE, "0"));
  return text.startsWith("-") ? -units : units;
}

function fromUnits(units: bigint): string {
  const absolute = units < 0n ? -units : units;
  return canonicalMoney(`${units < 0n ? "-" : ""}${absolute / UNIT}.${(absolute % UNIT).toString().padStart(SCALE, "0")}`);
}

export function sumMoney(values: MoneyAmount[]): string {
  return fromUnits(values.reduce<bigint>((total, value) => total + moneyUnits(value), 0n));
}

export function subtractMoney(left: MoneyAmount, right: MoneyAmount): string {
  return fromUnits(moneyUnits(left) - moneyUnits(right));
}

// Suggestions are display-only; floor to cents without floating-point math.
export function moneyShare(value: MoneyAmount, percent: number): string {
  if (!Number.isInteger(percent) || percent < 0 || percent > 100) throw new Error("Invalid share.");
  const cents = moneyUnits(value) * BigInt(percent) / (100n * 10n ** 26n);
  return fromUnits(cents * 10n ** 26n);
}

export function formatMoney(value: MoneyAmount): string {
  const text = canonicalMoney(value);
  const [whole, fraction] = text.split(".");
  const grouped = BigInt(whole).toLocaleString("en-US");
  const sign = text.startsWith("-0.") ? "-" : "";
  return `${sign}${grouped}${fraction ? `.${fraction}` : ""}`;
}

export function formatMoneyCurrency(value: MoneyAmount): string {
  const text = canonicalMoney(value);
  const [whole, fraction = ""] = text.replace(/^-/, "").split(".");
  return `${text.startsWith("-") ? "-" : ""}$${BigInt(whole).toLocaleString("en-US")}.${fraction.padEnd(2, "0")}`;
}
