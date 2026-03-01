/**
 * Utility helpers for the village mode.
 */

/** Format a number with K/M suffixes */
export function formatNumber(n: number): string {
  if (n >= 1_000_000) return (n / 1_000_000).toFixed(1) + 'M';
  if (n >= 10_000) return (n / 1_000).toFixed(1) + 'K';
  return Math.floor(n).toLocaleString('pt-PT');
}

/** Format production rate per hour */
export function formatProduction(perSecond: number): string {
  const perHour = perSecond * 3600;
  return `+${formatNumber(perHour)}/h`;
}

/** Lerp between two values */
export function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

/** Clamp a value */
export function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}
