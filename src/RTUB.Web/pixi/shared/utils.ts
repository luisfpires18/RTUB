/**
 * Shared utility functions used across all battle scenes.
 */
import type { BattleEvent } from '../types/battle-events';

/** Session-level cache bust — set once per page load. */
export const SESSION_CACHE_BUST = `?v=${Date.now()}`;

/** Track which asset paths are already loaded in PIXI.Assets. */
export const loadedAssetAliases = new Set<string>();

/** Cache decoded AudioBuffers so music files are only fetched/decoded once. */
export const audioBufferCache: Record<string, AudioBuffer> = {};

/** Cache-bust for audio files. */
export const audioCacheBuster = `?v=${Date.now()}`;

/**
 * Resolve a field from a battle event, handling PascalCase → camelCase → lowercase.
 */
export function getEventField<T = unknown>(
  evt: BattleEvent | null | undefined,
  field: string,
): T | undefined {
  if (!evt) return undefined;
  const record = evt as Record<string, unknown>;
  return (
    record[field] ??
    record[field[0].toLowerCase() + field.slice(1)] ??
    record[field.toLowerCase()]
  ) as T | undefined;
}

/**
 * Format a number with K/M/B/T abbreviation.
 */
export function formatNum(n: number | null | undefined): string {
  if (n == null) return '0';
  const abs = Math.abs(n);
  const sign = n < 0 ? '-' : '';
  if (abs >= 1e12)
    return sign + (abs / 1e12).toFixed(abs % 1e12 === 0 ? 0 : 2).replace(/\.?0+$/, '') + 'T';
  if (abs >= 1e9)
    return sign + (abs / 1e9).toFixed(abs % 1e9 === 0 ? 0 : 2).replace(/\.?0+$/, '') + 'B';
  if (abs >= 1e6)
    return sign + (abs / 1e6).toFixed(abs % 1e6 === 0 ? 0 : 2).replace(/\.?0+$/, '') + 'M';
  if (abs >= 1e3)
    return sign + (abs / 1e3).toFixed(abs % 1e3 === 0 ? 0 : 2).replace(/\.?0+$/, '') + 'K';
  return sign + Math.round(abs).toString();
}

/**
 * Resolve the events array from a Blazor battleData object.
 * Handles EventsJson string, arrays, and replay wrappers.
 */
export function resolveEvents(battleData: unknown): BattleEvent[] {
  if (!battleData) return [];

  const data = battleData as Record<string, unknown>;
  const eventsJson = (data.EventsJson ?? data.eventsJson ?? data.eventsjson) as
    | string
    | undefined;

  if (eventsJson && typeof eventsJson === 'string') {
    try {
      const parsed = JSON.parse(eventsJson) as unknown;
      if (Array.isArray(parsed)) return parsed as BattleEvent[];
      if (parsed && typeof parsed === 'object') {
        const obj = parsed as Record<string, unknown>;
        if (Array.isArray(obj.Events)) return obj.Events as BattleEvent[];
      }
      return [];
    } catch (e) {
      console.error('Failed to parse EventsJson:', e);
      return [];
    }
  }

  if (Array.isArray(battleData)) return battleData as BattleEvent[];

  return ((data.events ?? data.Events) as BattleEvent[] | undefined) ?? [];
}

/**
 * Resolve the DotNetObjectReference from a Blazor battleData object.
 */
export function resolveDotNetRef(
  battleData: unknown,
): DotNet.DotNetObject | null {
  if (!battleData || Array.isArray(battleData)) return null;
  const data = battleData as Record<string, unknown>;
  return (data.dotNetRef ?? data.DotNetRef ?? null) as DotNet.DotNetObject | null;
}

/**
 * Pick the first defined value from an object using PascalCase → camelCase fallback.
 */
export function pick<T>(
  data: Record<string, unknown> | null | undefined,
  pascal: string,
  camel: string,
  fallback: T,
): T {
  if (!data) return fallback;
  return ((data[pascal] ?? data[camel]) as T | undefined) ?? fallback;
}
