// The agenda's archive filters (URL <-> state, client-side filtering) and the season grouping.

import type { EventSummary } from './eventsApi';
import { fold } from './format';

export type Filters = { season: string; type: string; q: string; videos: boolean };

export function filtersFromUrl(): Filters {
  const p = new URLSearchParams(location.search);
  return { season: p.get('season') ?? '', type: p.get('type') ?? '', q: p.get('q') ?? '', videos: p.get('videos') === '1' };
}

export function filtersToUrl(f: Filters) {
  const url = new URL(location.href);
  for (const [key, value] of [['season', f.season], ['type', f.type], ['q', f.q.trim()], ['videos', f.videos ? '1' : '']]) {
    if (value) url.searchParams.set(key, value);
    else url.searchParams.delete(key);
  }
  history.replaceState(null, '', url.pathname + url.search);
}

/** The archive filters, all client-side: the agenda is small and already filtered by the server for the caller. */
export function filterPast(past: EventSummary[], f: Filters) {
  const q = fold(f.q.trim());
  return past.filter(
    (e) =>
      (!f.season || e.season === f.season) &&
      (!f.type || e.type === f.type) &&
      (!f.videos || e.videoCount > 0) &&
      (!q || [e.name, e.location, e.member?.description ?? ''].some((t) => fold(t).includes(q))),
  );
}

export function groupBySeason(events: EventSummary[]) {
  const groups: [string, EventSummary[]][] = [];
  for (const e of events) {
    const last = groups[groups.length - 1];
    if (last?.[0] === e.season) last[1].push(e);
    else groups.push([e.season, [e]]);
  }
  return groups;
}
