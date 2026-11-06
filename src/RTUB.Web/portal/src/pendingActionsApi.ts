import { call, type Outcome } from './api';

// /api/pending-actions (Endpoints/PendingActionsEndpoints.cs, task 071, docs/react-pending-actions.md). The server decides
// every panel, row, count and whether a row is required or may be dismissed (PendingActionsService); the page only shows.

export type ActionButton = { label: string; href: string; primary: boolean };
export type ActionRow = {
  kind: string;
  sourceKey: string;
  title: string;
  date: string | null;
  time: string | null;
  context: string | null;
  required: boolean;
  dismissible: boolean;
  actions: ActionButton[];
};
export type PanelFilter = { key: string; label: string; count: number };
export type Category = 'participation' | 'tasks' | 'responsibilities';
export type Panel = {
  key: string;
  title: string;
  category: Category;
  filters: PanelFilter[];
  filter: string | null;
  page: number;
  pageSize: number;
  total: number;
  rows: ActionRow[];
};
export type ActivityStatus = 'active' | 'retired' | 'none';
export type PendingActions = { today: string; fiscalYear: string; status: ActivityStatus; panels: Panel[] };

export const pendingActionsApi = {
  page: (): Promise<Outcome<PendingActions>> => call('GET', '/api/pending-actions'),
  panel: (key: string, filter: string | null, page: number): Promise<Outcome<Panel>> =>
    call('GET', `/api/pending-actions/panels/${encodeURIComponent(key)}?${new URLSearchParams({ ...(filter ? { filter } : {}), page: String(page) })}`),
  dismiss: (row: ActionRow): Promise<Outcome<void>> =>
    call('POST', '/api/pending-actions/dismissals', { kind: row.kind, sourceKey: row.sourceKey }),
};
