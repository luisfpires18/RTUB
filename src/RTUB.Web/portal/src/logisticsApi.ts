import { call, type Outcome } from './eventsApi';

// /api/logistics (Endpoints/LogisticsEndpoints.cs, React track 023). Signed-in members, Leitões refused unless they
// manage; Mod, Admin and Owner manage. The server decides every rule (LogisticsKanbanService).

export type LogisticsEvent = { id: number; name: string; date: string };
export type BoardSummary = { id: number; name: string; description: string; event: LogisticsEvent | null; isCompleted: boolean; completedAt: string | null };
export type Boards = { active: BoardSummary[]; completed: BoardSummary[]; canManage: boolean };
export type Label = { text: string; color: string };
export type Person = { displayName: string; fullName: string | null; avatarUrl: string | null };
export type Status = 'Todo' | 'InProgress' | 'Done';
export type Card = {
  id: number;
  title: string;
  description: string;
  status: Status;
  labels: Label[];
  startDate: string | null;
  dueDate: string | null;
  eventName: string | null;
  assignedTo: Person | null;
  assignments: number;
  checklistDone: number;
  checklistTotal: number;
  links: number;
};
export type List = { id: number; name: string; cards: Card[] };
export type BoardFile = { name: string; extension: string; sizeBytes: number };
export type Board = {
  id: number;
  name: string;
  description: string;
  event: LogisticsEvent | null;
  isCompleted: boolean;
  lists: List[];
  labels: string[];
  files: BoardFile[];
  canManage: boolean;
};
export type Assignee = { userId: string; displayName: string; fullName: string | null; avatarUrl: string | null };
export type ChecklistItem = { task: string; done: boolean };
export type CardDetail = {
  card: Card;
  listId: number;
  assignedTo: Assignee | null;
  checklist: ChecklistItem[];
  links: { cardId: number | null; name: string }[];
  assignments: Assignee[];
};
export type MemberOption = { id: string; displayName: string; fullName: string | null; avatarUrl: string | null };
export type CardInput = { title: string; description: string; startDate: string | null; dueDate: string | null; assignedToUserId: string | null };

const q = (params: Record<string, string>) => {
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v) p.set(k, v);
  const s = p.toString();
  return s ? `?${s}` : '';
};

export const logisticsApi = {
  boards: (search: string) => call<Boards>('GET', `/api/logistics${q({ q: search })}`),
  events: (search: string) => call<LogisticsEvent[]>('GET', `/api/logistics/events${q({ q: search })}`),
  members: (search: string) => call<MemberOption[]>('GET', `/api/logistics/members${q({ q: search })}`),
  createBoard: (input: { name: string; description: string; eventId: number | null }) => call<BoardSummary>('POST', '/api/logistics/boards', input),
  updateBoard: (id: number, input: { name: string; description: string; eventId: number | null }) => call<BoardSummary>('PUT', `/api/logistics/boards/${id}`, input),
  setBoardState: (id: number, completed: boolean) => call<BoardSummary>('POST', `/api/logistics/boards/${id}/state`, { completed }),
  removeBoard: (id: number) => call<void>('DELETE', `/api/logistics/boards/${id}`),

  board: (id: number) => call<Board>('GET', `/api/logistics/boards/${id}`),
  createList: (boardId: number, name: string) => call<List>('POST', `/api/logistics/boards/${boardId}/lists`, { name }),
  renameList: (id: number, name: string) => call<List>('PUT', `/api/logistics/lists/${id}`, { name }),
  moveList: (id: number, position: number) => call<void>('POST', `/api/logistics/lists/${id}/move`, { position }),
  removeList: (id: number) => call<void>('DELETE', `/api/logistics/lists/${id}`),

  createCard: (listId: number, input: { title: string; description: string; assignedToUserId: string | null }) =>
    call<CardDetail>('POST', `/api/logistics/lists/${listId}/cards`, input),
  card: (id: number) => call<CardDetail>('GET', `/api/logistics/cards/${id}`),
  updateCard: (id: number, input: CardInput) => call<CardDetail>('PUT', `/api/logistics/cards/${id}`, input),
  setStatus: (id: number, status: Status) => call<CardDetail>('POST', `/api/logistics/cards/${id}/status`, { status }),
  moveCard: (id: number, listId: number, position: number) => call<void>('POST', `/api/logistics/cards/${id}/move`, { listId, position }),
  setLabels: (id: number, labels: Label[]) => call<CardDetail>('PUT', `/api/logistics/cards/${id}/labels`, { labels }),
  setChecklist: (id: number, items: ChecklistItem[]) => call<CardDetail>('PUT', `/api/logistics/cards/${id}/checklist`, { items }),
  setLinks: (id: number, cardIds: number[]) => call<CardDetail>('PUT', `/api/logistics/cards/${id}/links`, { cardIds }),
  assign: (id: number, userId: string) => call<CardDetail>('POST', `/api/logistics/cards/${id}/assignments`, { userId }),
  unassign: (id: number, userId: string) => call<CardDetail>('DELETE', `/api/logistics/cards/${id}/assignments/${encodeURIComponent(userId)}`),
  removeCard: (id: number) => call<void>('DELETE', `/api/logistics/cards/${id}`),

  createReminder: (boardId: number, input: { cardId: number; frequency: string; nextReminderDate: string; userIds: string[] }) =>
    call<unknown>('POST', `/api/logistics/boards/${boardId}/reminders`, input),
  file: (boardId: number, name: string) => call<{ url: string }>('GET', `/api/logistics/boards/${boardId}/files${q({ name })}`),
  upload: (boardId: number, file: File) => {
    const form = new FormData();
    form.append('file', file, file.name);
    return call<BoardFile>('POST', `/api/logistics/boards/${boardId}/files`, form);
  },
  removeFile: (boardId: number, name: string) => call<void>('DELETE', `/api/logistics/boards/${boardId}/files${q({ name })}`),
};

export const STATUS: Record<Status, { label: string; name: string }> = {
  Todo: { label: 'TODO', name: 'Por fazer' },
  InProgress: { label: 'WIP', name: 'Em curso' },
  Done: { label: 'DONE', name: 'Feito' },
};

export const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Só Mod, Admin ou Owner gerem a logística.'
      : o.kind === 'notfound'
        ? 'Isto já não existe. Recarrega a página.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

/** "2026-10-04T00:00:00" → "04/10/2026". */
export const day = (iso: string | null) => (iso ? iso.slice(0, 10).split('-').reverse().join('/') : '');
