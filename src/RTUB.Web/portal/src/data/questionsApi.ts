// Types, API calls and display helpers for the /questions page.
import { call, type Outcome, outcomeText } from './api';

// /api/questions (Endpoints/QuestionEndpoints.cs). Dates are UTC.
type Status = 'unanswered' | 'answered' | 'inDiscussion' | 'closed';

type Person = { displayName: string; fullName: string | null; avatarUrl: string | null };

export type Question = {
  id: number;
  title: string;
  content: string;
  author: Person;
  recipient: { displayName: string; avatarUrl: string | null; role: string };
  status: Status;
  createdAt: string;
  lastActivityAt: string;
  replyCount: number;
  isMine: boolean;
  canReply: boolean;
  canRemind: boolean;
};

export type QuestionPage = { items: Question[]; total: number; page: number; pageSize: number };

type Reply = { id: number; content: string; author: Person; fromRecipient: boolean; createdAt: string };

export type Detail = { question: Question; replies: Reply[] };

export type Recipient = { id: string; displayName: string; fullName: string | null; avatarUrl: string | null; position: string; role: string };

export const api = {
  page: (closed: boolean, q: string, recipient: string, page: number) => {
    const p = new URLSearchParams({ closed: String(closed), page: String(page), pageSize: String(PAGE_SIZE) });
    if (q) p.set('q', q);
    if (recipient) p.set('recipient', recipient);
    return call<QuestionPage>('GET', `/api/questions?${p}`);
  },
  recipients: () => call<Recipient[]>('GET', '/api/questions/recipients'),
  detail: (id: number) => call<Detail>('GET', `/api/questions/${id}`),
  ask: (input: { title: string; content: string; recipientId: string; position: string }) => call<Detail>('POST', '/api/questions', input),
  reply: (id: number, content: string) => call<Detail>('POST', `/api/questions/${id}/replies`, { content }),
  close: (id: number) => call<Detail>('POST', `/api/questions/${id}/close`),
  remind: (id: number) => call<void>('POST', `/api/questions/${id}/remind`),
  remove: (id: number) => call<void>('DELETE', `/api/questions/${id}`),
};

const PAGE_SIZE = 10;

export const MAX_TITLE = 100;

export const MIN_CONTENT = 10;

export const MAX_CONTENT = 5000;

export const statusLabel: Record<Status, string> = {
  unanswered: 'Aguarda resposta',
  answered: 'Respondida',
  inDiscussion: 'Em discussão',
  closed: 'Fechada',
};

export const statusPill: Record<Status, string> = {
  unanswered: 'pill pill--wait',
  answered: 'pill pill--yes',
  inDiscussion: 'pill pill--wait',
  closed: 'pill',
};

export const stamp = (iso: string) =>
  new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`).toLocaleString('pt-PT', { dateStyle: 'medium', timeStyle: 'short' });

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, { forbidden: 'Não tem permissão para esta ação.', notfound: 'Esta pergunta já não existe.' });
