import { call, type Outcome } from './api';

// /api/pending-actions (Endpoints/PendingActionsEndpoints.cs, task 071, docs/react-pending-actions.md). The server decides
// every panel, row, count and whether a row is required or may be dismissed (PendingActionsService); the page only shows.

/** A link to the module's page, or (task 072) an in-page answer sent to /responses. */
export type ActionButton = { label: string; href: string | null; primary: boolean; answer: 'yes' | 'no' | null };
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
  /** The item's own page (the title links there). */
  href: string | null;
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
export type InstrumentOption = { value: string; label: string };
export type PendingActions = {
  today: string;
  fiscalYear: string;
  status: ActivityStatus;
  panels: Panel[];
  /** What a Vou / Fui may name (the module's rule); the primary is the default. */
  instruments: InstrumentOption[];
  defaultInstrument: string | null;
};
export type Answer = { answer: 'yes' | 'no'; instrument?: string | null; notes?: string | null };

export const pendingActionsApi = {
  page: (): Promise<Outcome<PendingActions>> => call('GET', '/api/pending-actions'),
  panel: (key: string, filter: string | null, page: number): Promise<Outcome<Panel>> =>
    call('GET', `/api/pending-actions/panels/${encodeURIComponent(key)}?${new URLSearchParams({ ...(filter ? { filter } : {}), page: String(page) })}`),
  dismiss: (row: ActionRow): Promise<Outcome<void>> =>
    call('POST', '/api/pending-actions/dismissals', { kind: row.kind, sourceKey: row.sourceKey }),
  /** 204 saved; 404 no longer pending (answered elsewhere, cancelled); 409 answered meanwhile; 400 invalid instrument / note. */
  respond: (row: ActionRow, a: Answer): Promise<Outcome<void>> =>
    call('POST', '/api/pending-actions/responses', { kind: row.kind, sourceKey: row.sourceKey, ...a }),
};
