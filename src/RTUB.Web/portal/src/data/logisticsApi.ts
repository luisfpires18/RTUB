import { antiforgeryToken, queryString, outcomeText } from './api';
import { call, type Outcome } from './eventsApi';

// /api/logistics (Endpoints/LogisticsEndpoints.cs, React track 023). Signed-in members read (Leitões too since 065);
// assignees tick checklists and change status; Mod, Admin and Owner manage. The server decides every rule
// (LogisticsKanbanService).

import type { Board, BoardFile, BoardInput, BoardSummary, Boards, CardDetail, CardInput, ComposeInput, Frequency, Label, List, LogisticsEvent, MemberOption, MyTasks, Priority, ReminderInput, Status, Template } from './logisticsTypes';

export * from './logisticsTypes';


export const logisticsApi = {
  boards: (search: string) => call<Boards>('GET', `/api/logistics${queryString({ q: search })}`),
  events: (search: string) => call<LogisticsEvent[]>('GET', `/api/logistics/events${queryString({ q: search })}`),
  /** With `boardId`: only members who may see that board (task 067). */
  members: (search: string, boardId?: number) => call<MemberOption[]>('GET', `/api/logistics/members${queryString({ q: search, boardId: boardId ? String(boardId) : '' })}`),
  createBoard: (input: BoardInput) => call<BoardSummary>('POST', '/api/logistics/boards', input),
  updateBoard: (id: number, input: BoardInput) => call<BoardSummary>('PUT', `/api/logistics/boards/${id}`, input),
  setBoardState: (id: number, completed: boolean) => call<BoardSummary>('POST', `/api/logistics/boards/${id}/state`, { completed }),
  archiveBoard: (id: number, archived: boolean) => call<BoardSummary>('POST', `/api/logistics/boards/${id}/archive`, { archived }),
  duplicateBoard: (id: number, name: string) => call<BoardSummary>('POST', `/api/logistics/boards/${id}/duplicate`, { name }),
  templates: () => call<Template[]>('GET', '/api/logistics/templates'),
  compose: (input: ComposeInput) => call<BoardSummary>('POST', '/api/logistics/boards/compose', input),
  removeBoard: (id: number) => call<void>('DELETE', `/api/logistics/boards/${id}`),

  // Without the files (a storage round trip): fileList brings them on a second request.
  board: (id: number) => call<Board>('GET', `/api/logistics/boards/${id}?files=false`),
  fileList: (id: number) => call<BoardFile[]>('GET', `/api/logistics/boards/${id}/file-list`),
  createList: (boardId: number, name: string, state: Status) => call<List>('POST', `/api/logistics/boards/${boardId}/lists`, { name, state }),
  renameList: (id: number, name: string, state: Status) => call<List>('PUT', `/api/logistics/lists/${id}`, { name, state }),
  moveList: (id: number, position: number) => call<void>('POST', `/api/logistics/lists/${id}/move`, { position }),
  /** A column with cards needs `moveTo` (another column of the board); its cards take that column's state. */
  removeList: (id: number, moveTo?: number) => call<void>('DELETE', `/api/logistics/lists/${id}${queryString({ moveTo: moveTo ? String(moveTo) : '' })}`),

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
    call<CardDetail>('DELETE', `/api/logistics/cards/${id}/checklist/${index}${queryString({ task: expected })}`),
  /** One item's tick (assignees too); `task` must still be the item's text. */
  toggleItem: (id: number, index: number, task: string, done: boolean) =>
    call<CardDetail>('POST', `/api/logistics/cards/${id}/checklist/${index}`, { task, done }),
  setLinks: (id: number, cardIds: number[]) => call<CardDetail>('PUT', `/api/logistics/cards/${id}/links`, { cardIds }),
  assign: (id: number, userId: string) => call<CardDetail>('POST', `/api/logistics/cards/${id}/assignments`, { userId }),
  unassign: (id: number, userId: string) => call<CardDetail>('DELETE', `/api/logistics/cards/${id}/assignments/${encodeURIComponent(userId)}`),
  removeCard: (id: number) => call<void>('DELETE', `/api/logistics/cards/${id}`),
  archiveCard: (id: number, archived: boolean) => call<CardDetail>('POST', `/api/logistics/cards/${id}/archive`, { archived }),
  duplicateCard: (id: number) => call<CardDetail>('POST', `/api/logistics/cards/${id}/duplicate`),

  comment: (id: number, body: string, mentionUserIds: string[]) => call<CardDetail>('POST', `/api/logistics/cards/${id}/comments`, { body, mentionUserIds }),
  editComment: (commentId: number, body: string) => call<CardDetail>('PUT', `/api/logistics/comments/${commentId}`, { body }),
  removeComment: (commentId: number) => call<CardDetail>('DELETE', `/api/logistics/comments/${commentId}`),
  addLink: (id: number, url: string, name: string) => call<CardDetail>('POST', `/api/logistics/cards/${id}/links`, { url, name }),
  attachment: (id: number, attachmentId: number) => call<{ url: string }>('GET', `/api/logistics/cards/${id}/attachments/${attachmentId}`),
  removeAttachment: (id: number, attachmentId: number) => call<CardDetail>('DELETE', `/api/logistics/cards/${id}/attachments/${attachmentId}`),

  createReminder: (cardId: number, input: ReminderInput) => call<CardDetail>('POST', `/api/logistics/cards/${cardId}/reminders`, input),
  updateReminder: (cardId: number, id: number, input: ReminderInput) =>
    call<CardDetail>('PUT', `/api/logistics/cards/${cardId}/reminders/${id}`, input),
  removeReminder: (cardId: number, id: number) => call<CardDetail>('DELETE', `/api/logistics/cards/${cardId}/reminders/${id}`),
  myTasks: () => call<MyTasks>('GET', '/api/logistics/my-tasks'),
  file: (boardId: number, id: string) => call<{ url: string }>('GET', `/api/logistics/boards/${boardId}/files${queryString({ file: id })}`),
  upload: (boardId: number, file: File) => {
    const form = new FormData();
    form.append('file', file, file.name);
    return call<BoardFile>('POST', `/api/logistics/boards/${boardId}/files`, form);
  },
  removeFile: (boardId: number, id: string) => call<void>('DELETE', `/api/logistics/boards/${boardId}/files${queryString({ file: id })}`),
};

export const STATUS: Record<Status, { label: string; name: string }> = {
  Todo: { label: 'TODO', name: 'Por fazer' },
  InProgress: { label: 'WIP', name: 'Em curso' },
  Blocked: { label: 'BLOQ', name: 'Bloqueado' },
  Done: { label: 'DONE', name: 'Feito' },
};

export const PRIORITY: Record<Priority, string> = { Low: 'Baixa', Normal: 'Normal', High: 'Alta', Urgent: 'Urgente' };

/** Card states in board order. */
export const STATUSES: Status[] = ['Todo', 'InProgress', 'Blocked', 'Done'];

/** Priorities for pickers, lowest first. */
export const PRIORITIES_LOW_FIRST: Priority[] = ['Low', 'Normal', 'High', 'Urgent'];

/** Priorities for filters and sorting, most urgent first. */
export const PRIORITIES_URGENT_FIRST: Priority[] = ['Urgent', 'High', 'Normal', 'Low'];

export const FREQUENCY: Record<Frequency, string> = { OneTime: 'Uma vez', Daily: 'Todos os dias', Weekly: 'Todas as semanas' };

/** A card on its board: the board page opens it from ?card= (task 069; pushes link here too). */
export const cardUrl = (boardId: number, cardId: number) => `/logistics/${boardId}?card=${cardId}`;

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, { conflict: 'O cartão foi alterado por outra pessoa entretanto.', closed: 'Está arquivado: só leitura.', forbidden: 'Não tem permissão para fazer isto.', notfound: 'Isto já não existe. Recarrega a página.' });


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
