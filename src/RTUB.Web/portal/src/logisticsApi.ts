import { antiforgeryToken } from './api';
import { call, type Outcome } from './eventsApi';

// /api/logistics (Endpoints/LogisticsEndpoints.cs, React track 023). Signed-in members read (Leitões too since 065);
// assignees tick checklists and change status; Mod, Admin and Owner manage. The server decides every rule
// (LogisticsKanbanService).

export type LogisticsEvent = { id: number; name: string; date: string };
export type BoardSummary = { id: number; name: string; description: string; event: LogisticsEvent | null; isCompleted: boolean; completedAt: string | null };
export type Boards = { active: BoardSummary[]; completed: BoardSummary[]; canManage: boolean };
export type Label = { text: string; color: string };
export type Person = { displayName: string; fullName: string | null; avatarUrl: string | null };
export type Status = 'Todo' | 'InProgress' | 'Done';
export type Priority = 'Low' | 'Normal' | 'High' | 'Urgent';
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
  /** The caller is the owner or a collaborator (task 065). */
  mine: boolean;
  priority: Priority;
  comments: number;
  attachments: number;
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
/** `userId` only for managers (they change assignments); null for everyone else. */
export type Assignee = { userId: string | null; displayName: string; fullName: string | null; avatarUrl: string | null };
export type ChecklistItem = { task: string; done: boolean };
export type CardDetail = {
  card: Card;
  listId: number;
  assignedTo: Assignee | null;
  checklist: ChecklistItem[];
  links: { cardId: number | null; name: string }[];
  assignments: Assignee[];
  /** May change everything (Mod, Admin, Owner): owner, collaborators, labels, card links, delete. */
  canManage: boolean;
  /** Owner, collaborator or manager (task 066): edits the card's fields and checklist, status, comments, attaches. */
  canWork: boolean;
  /** Sent back with a field edit; a stale one answers 409 ('conflict'). */
  version: number;
  comments: CardComment[];
  attachments: CardAttachment[];
  activity: CardActivity[];
};
export type CardComment = {
  id: number;
  author: Person | null;
  body: string;
  mentions: string[];
  createdAt: string;
  editedAt: string | null;
  canEdit: boolean;
  canDelete: boolean;
};
export type CardAttachment = {
  id: number;
  kind: 'File' | 'Link';
  name: string;
  host: string | null;
  sizeBytes: number;
  extension: string | null;
  addedBy: Person | null;
  createdAt: string;
  canDelete: boolean;
};
export type CardActivity = { actor: Person | null; action: string; detail: string; at: string };
export type MemberOption = { id: string; displayName: string; fullName: string | null; avatarUrl: string | null };
export type CardInput = {
  title: string;
  description: string;
  startDate: string | null;
  dueDate: string | null;
  /** The owner: only a manager changes or clears it; everyone else sends null (kept). */
  assignedToUserId: string | null;
  priority: Priority;
  version: number;
};

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

  // Without the files (a storage round trip): fileList brings them on a second request.
  board: (id: number) => call<Board>('GET', `/api/logistics/boards/${id}?files=false`),
  fileList: (id: number) => call<BoardFile[]>('GET', `/api/logistics/boards/${id}/file-list`),
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
  // The checklist one item at a time (task 066): the item's text identifies it, so two people never overwrite each other.
  addItem: (id: number, task: string) => call<CardDetail>('POST', `/api/logistics/cards/${id}/checklist`, { task }),
  renameItem: (id: number, index: number, expected: string, task: string) =>
    call<CardDetail>('PUT', `/api/logistics/cards/${id}/checklist/${index}`, { expected, task }),
  removeItem: (id: number, index: number, expected: string) =>
    call<CardDetail>('DELETE', `/api/logistics/cards/${id}/checklist/${index}${q({ task: expected })}`),
  /** One item's tick (assignees too); `task` must still be the item's text. */
  toggleItem: (id: number, index: number, task: string, done: boolean) =>
    call<CardDetail>('POST', `/api/logistics/cards/${id}/checklist/${index}`, { task, done }),
  setLinks: (id: number, cardIds: number[]) => call<CardDetail>('PUT', `/api/logistics/cards/${id}/links`, { cardIds }),
  assign: (id: number, userId: string) => call<CardDetail>('POST', `/api/logistics/cards/${id}/assignments`, { userId }),
  unassign: (id: number, userId: string) => call<CardDetail>('DELETE', `/api/logistics/cards/${id}/assignments/${encodeURIComponent(userId)}`),
  removeCard: (id: number) => call<void>('DELETE', `/api/logistics/cards/${id}`),

  comment: (id: number, body: string, mentionUserIds: string[]) => call<CardDetail>('POST', `/api/logistics/cards/${id}/comments`, { body, mentionUserIds }),
  editComment: (commentId: number, body: string) => call<CardDetail>('PUT', `/api/logistics/comments/${commentId}`, { body }),
  removeComment: (commentId: number) => call<CardDetail>('DELETE', `/api/logistics/comments/${commentId}`),
  addLink: (id: number, url: string, name: string) => call<CardDetail>('POST', `/api/logistics/cards/${id}/links`, { url, name }),
  attachment: (id: number, attachmentId: number) => call<{ url: string }>('GET', `/api/logistics/cards/${id}/attachments/${attachmentId}`),
  removeAttachment: (id: number, attachmentId: number) => call<CardDetail>('DELETE', `/api/logistics/cards/${id}/attachments/${attachmentId}`),

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

export const PRIORITY: Record<Priority, string> = { Low: 'Baixa', Normal: 'Normal', High: 'Alta', Urgent: 'Urgente' };

export const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'conflict'
    ? 'O cartão foi alterado por outra pessoa entretanto.'
    : o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Sem permissão: só o responsável, os colaboradores, Mod, Admin ou Owner.'
      : o.kind === 'notfound'
        ? 'Isto já não existe. Recarrega a página.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

/** "2026-10-04T00:00:00" → "04/10/2026". */
export const day = (iso: string | null) => (iso ? iso.slice(0, 10).split('-').reverse().join('/') : '');

/**
 * A card file (task 066), through XHR so the upload reports its progress. Same outcomes as `call`; a refused antiforgery
 * token is fetched again once.
 */
export async function uploadAttachment(cardId: number, file: File, onProgress: (share: number) => void, retried = false): Promise<Outcome<CardDetail>> {
  let token: string;
  try {
    token = await antiforgeryToken(retried);
  } catch {
    return { kind: 'failed' };
  }

  const body = new FormData();
  body.append('file', file, file.name);
  const answer = await new Promise<{ status: number; text: string }>((resolve) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', `/api/logistics/cards/${cardId}/attachments`);
    xhr.setRequestHeader('X-CSRF-TOKEN', token);
    xhr.setRequestHeader('Accept', 'application/json');
    xhr.upload.onprogress = (e) => e.lengthComputable && onProgress(e.loaded / e.total);
    xhr.onload = () => resolve({ status: xhr.status, text: xhr.responseText });
    xhr.onerror = () => resolve({ status: 0, text: '' });
    xhr.send(body);
  });

  const json = (() => {
    try {
      return JSON.parse(answer.text) as CardDetail & { errors?: Record<string, string[]> };
    } catch {
      return null;
    }
  })();
  if (answer.status === 200 && json) return { kind: 'ok', data: json };
  if (answer.status === 400 && json?.errors) {
    const errors: Record<string, string> = {};
    for (const [k, v] of Object.entries(json.errors)) errors[k.charAt(0).toLowerCase() + k.slice(1)] = v[0];
    return { kind: 'invalid', errors };
  }
  if (answer.status === 400 && !retried) return uploadAttachment(cardId, file, onProgress, true);
  if (answer.status === 413) return { kind: 'invalid', errors: { file: 'O ficheiro é demasiado grande. Tamanho máximo: 10MB' } };
  if (answer.status === 401) return { kind: 'signin' };
  if (answer.status === 403) return { kind: 'forbidden' };
  if (answer.status === 404) return { kind: 'notfound' };
  return { kind: 'failed' };
}
