// The board page's view, filters and open card kept in the address.
import { PRIORITIES_URGENT_FIRST, STATUSES, type Priority, type Status } from './logisticsApi';
import { lisbonToday } from './format';
import type { Due, View } from './logisticsBoardTypes';
import type { Sort } from './logisticsRows';

/**
 * What the address keeps - the view, the filters, the list order, the calendar month and the open card - so
 * refresh and back restore the board as it was. Only the member's own choices, and nothing from a card but its id. A
 * person filter is "mine" / "none", or "person" for a named member: the name never goes in the address (a link may be
 * shared). It is kept in that history entry's own state (history.state, per entry, kept by the browser across refresh
 * and Back), so each entry restores the person chosen in it; a shared link or a fresh visit has no state, so no person
 * filter.
 */
const PERSON = 'rtubLogisticsPerson';
const entryPerson = (): string => {
  const state = history.state as Record<string, unknown> | null;
  return typeof state?.[PERSON] === 'string' ? (state[PERSON] as string) : '';
};
type UrlState = { view: View; q: string; status: Status | ''; label: string; who: string; prio: Priority | ''; due: Due; sort: Sort; month: string; card?: number };
/** Everything the address keeps except the open card, which the page holds as its own state. */
export type BoardFilters = Omit<UrlState, 'card'>;
export const withoutCard = ({ card: _card, ...filters }: UrlState): BoardFilters => filters;
export const readUrl = (): UrlState => {
  const p = new URLSearchParams(location.search);
  const pick = <T extends string>(key: string, allowed: readonly T[], fallback: T) => (allowed.includes(p.get(key) as T) ? (p.get(key) as T) : fallback);
  const card = Number(p.get('card'));
  return {
    view: pick<View>('view', ['board', 'list', 'calendar'], 'board'),
    q: p.get('q') ?? '',
    status: pick<Status | ''>('status', ['', ...STATUSES], ''),
    label: p.get('label') ?? '',
    who: ((w) => (w === 'person' ? entryPerson() : w))(pick('who', ['', 'mine', 'none', 'person'], '')),
    prio: pick<Priority | ''>('prio', ['', ...PRIORITIES_URGENT_FIRST], ''),
    due: pick<Due>('due', ['', 'late', 'soon', 'none'], ''),
    sort: pick<Sort>('sort', ['due', 'priority', 'status', 'title'], 'due'),
    month: /^\d{4}-(0[1-9]|1[0-2])$/.test(p.get('month') ?? '') ? p.get('month')! : lisbonToday().slice(0, 7),
    card: Number.isInteger(card) && card > 0 ? card : undefined,
  };
};
export const writeUrl = (u: UrlState, push: boolean) => {
  const p = new URLSearchParams();
  if (u.view !== 'board') p.set('view', u.view);
  if (u.q) p.set('q', u.q);
  if (u.status) p.set('status', u.status);
  if (u.label) p.set('label', u.label);
  const person = u.who && u.who !== 'mine' && u.who !== 'none' ? u.who : '';
  if (u.who) p.set('who', person ? 'person' : u.who);
  if (u.prio) p.set('prio', u.prio);
  if (u.due) p.set('due', u.due);
  if (u.view === 'list' && u.sort !== 'due') p.set('sort', u.sort);
  if (u.view === 'calendar' && u.month !== lisbonToday().slice(0, 7)) p.set('month', u.month);
  if (u.card) p.set('card', String(u.card));
  const query = p.toString();
  const next = `${location.pathname}${query ? `?${query}` : ''}`;
  if (next === `${location.pathname}${location.search}` && entryPerson() === person) return;
  // The entry's state carries the person's name (or none); everything else in it is kept.
  const state = { ...((history.state as Record<string, unknown> | null) ?? {}), [PERSON]: person || undefined };
  if (push) history.pushState(state, '', next);
  else history.replaceState(state, '', next);
};
