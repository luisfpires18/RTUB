// Rows of the board list view: the card with its list, the sort orders and the "late" test.
import { PRIORITIES_URGENT_FIRST, STATUSES, type Card, type List } from './logisticsApi';
import { lisbonToday } from './format';

export type Row = { card: Card; list: List };
export type Sort = 'due' | 'priority' | 'status' | 'title';

/** Today in Portugal, "yyyy-mm-dd" (deadlines are Portuguese calendar days). */
export const dueDay = (c: Card) => c.dueDate?.slice(0, 10) ?? null;
export const isLate = (c: Card, today = lisbonToday()) => c.status !== 'Done' && !!dueDay(c) && dueDay(c)! < today;


const pt = new Intl.Collator('pt-PT', { sensitivity: 'base' });

export function sortRows(rows: Row[], sort: Sort): Row[] {
  const byTitle = (a: Row, b: Row) => pt.compare(a.card.title, b.card.title);
  const by: Record<Sort, (a: Row, b: Row) => number> = {
    // Undated last; ties by title.
    due: (a, b) => (dueDay(a.card) ?? '9999').localeCompare(dueDay(b.card) ?? '9999') || byTitle(a, b),
    priority: (a, b) => PRIORITIES_URGENT_FIRST.indexOf(a.card.priority) - PRIORITIES_URGENT_FIRST.indexOf(b.card.priority) || byTitle(a, b),
    status: (a, b) => STATUSES.indexOf(a.card.status) - STATUSES.indexOf(b.card.status) || byTitle(a, b),
    title: byTitle,
  };
  return [...rows].sort(by[sort]);
}
