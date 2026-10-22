const MAX_UINT256 = (1n << 256n) - 1n;

// Financial text is never rounded. Extra non-zero fractional digits are errors.
export function decimalToBaseUnits(amount: string | number, decimals: number): bigint {
  if (!Number.isInteger(decimals) || decimals < 0 || decimals > 18) {
    throw new Error("Token decimals must be between 0 and 18.");
  }
  if (typeof amount === "number" && (!Number.isFinite(amount) || amount < 0 || amount > Number.MAX_SAFE_INTEGER)) {
    throw new Error("Use an exact decimal amount within the supported range.");
  }
  let text = String(amount).trim();
  // JSON numbers may use exponents for small amounts. Expand the canonical
  // decimal representation; toFixed would expose binary floating-point digits.
  if (typeof amount === "number" && /e/i.test(text)) {
    const [mantissa, exponent] = text.toLowerCase().split("e");
    const [whole, fraction = ""] = mantissa.split(".");
    const digits = whole + fraction;
    const position = whole.length + Number(exponent);
    text = position <= 0
      ? `0.${"0".repeat(-position)}${digits}`
      : position >= digits.length
        ? digits + "0".repeat(position - digits.length)
        : `${digits.slice(0, position)}.${digits.slice(position)}`;
  }
  if (!/^\d+(?:\.\d+)?$/.test(text)) {
    throw new Error("Enter a non-negative decimal amount without separators or exponents.");
  }
  const [whole, fraction = ""] = text.split(".");
  if (/[1-9]/.test(fraction.slice(decimals))) {
    throw new Error(`This amount cannot be represented exactly with ${decimals} token decimals.`);
  }
  const units = BigInt(whole) * 10n ** BigInt(decimals)
    + BigInt(fraction.slice(0, decimals).padEnd(decimals, "0") || "0");
  if (units > MAX_UINT256) throw new Error("This amount is too large for the token contract.");
  return units;
}

// API request shapes still use JSON numbers. Refuse text whose cents would
// change during that conversion instead of silently sending another amount.
export function isExactJsonMoneyInput(text: string): boolean {
  try {
    const cents = decimalToBaseUnits(text, 2);
    return cents <= BigInt(Number.MAX_SAFE_INTEGER)
      && decimalToBaseUnits(Number(text), 2) === cents;
  } catch {
    return false;
  }
}

export function formatBaseUnits(units: bigint, decimals: number): string {
  if (units < 0n || !Number.isInteger(decimals) || decimals < 0 || decimals > 18) {
    throw new Error("Invalid token amount or decimals.");
  }
  const scale = 10n ** BigInt(decimals);
  const whole = (units / scale).toLocaleString("en-US");
  const fraction = (units % scale).toString().padStart(decimals, "0").replace(/0+$/, "");
  return fraction ? `${whole}.${fraction}` : whole;
}
