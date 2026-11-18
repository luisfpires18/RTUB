// Types, API calls and date/sort helpers for the /messages page.
import { call, type Outcome, outcomeText } from './api';

// /api/conversations + /api/messages (Endpoints/MessagingEndpoints.cs): Messaging v2 over the preserved v1
// tables. The server decides every rule (MessagingService): a conversation the member is not in is a 404, and
// `canSend` only spares a composer that would be refused. Text only. Dates are UTC.
export type Person = { id: string; displayName: string; fullName: string | null; avatarUrl: string };

type Kind = 'direct' | 'group' | 'system';

export type ConversationItem = {
  id: number;
  kind: Kind;
  title: string;
  avatarUrl: string;
  lastMessagePreview: string | null;
  lastMessageIsMine: boolean;
  lastMessageAt: string;
  unreadCount: number;
  muted: boolean;
  pinned: boolean;
  canSend: boolean;
};

type Reaction = { emoji: string; count: number; mine: boolean };

export type MessageItem = {
  id: number;
  conversationId: number;
  sender: Person | null;
  isMine: boolean;
  isSystem: boolean;
  body: string;
  link: string | null;
  createdAt: string;
  replyTo: { messageId: number | null; senderName: string | null; body: string } | null;
  reactions: Reaction[];
  seen: boolean;
};

export type Detail = {
  conversation: ConversationItem;
  participants: Person[];
  messages: MessageItem[];
  hasMore: boolean;
  reactionChoices: string[];
};

export const api = {
  list: () => call<ConversationItem[]>('GET', '/api/conversations'),
  recipients: () => call<Person[]>('GET', '/api/conversations/recipients'),
  detail: (id: number, before?: number) => call<Detail>('GET', `/api/conversations/${id}${before ? `?before=${before}` : ''}`),
  direct: (userId: string) => call<{ id: number | null }>('GET', `/api/conversations/direct/${encodeURIComponent(userId)}`),
  // Nothing is stored before the first message: the conversation and that message are created together.
  start: (userIds: string[], title: string, body: string) => call<Detail>('POST', '/api/conversations', { userIds, title, body }),
  send: (id: number, body: string, replyToMessageId: number | null) =>
    call<MessageItem>('POST', `/api/conversations/${id}/messages`, { body, replyToMessageId }),
  remove: (id: number) => call<void>('DELETE', `/api/conversations/${id}`),
  read: (id: number) => call<void>('POST', `/api/conversations/${id}/read`),
  mute: (id: number, muted: boolean) => call<{ muted: boolean }>('POST', `/api/conversations/${id}/${muted ? 'mute' : 'unmute'}`),
  react: (messageId: number, emoji: string) => call<MessageItem>('POST', `/api/messages/${messageId}/reactions`, { emoji }),
  unreact: (messageId: number) => call<MessageItem>('DELETE', `/api/messages/${messageId}/reactions`),
};

export const MAX_BODY = 2000;

export const THREAD_POLL_MS = 10_000;

export const LIST_POLL_MS = 30_000;

export const toDate = (iso: string) => new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`);

export const sameDay = (a: Date, b: Date) => a.toDateString() === b.toDateString();

export const clock = (iso: string) => toDate(iso).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });

export const listStamp = (iso: string) => {
  const d = toDate(iso);
  return sameDay(d, new Date()) ? clock(iso) : d.toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit' });
};

export const dayLabel = (iso: string) => {
  const d = toDate(iso);
  const today = new Date();
  const yesterday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 1);
  if (sameDay(d, today)) return 'Hoje';
  if (sameDay(d, yesterday)) return 'Ontem';
  return d.toLocaleDateString('pt-PT', { day: 'numeric', month: 'long', year: d.getFullYear() === today.getFullYear() ? undefined : 'numeric' });
};

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, { forbidden: 'Não podes escrever nesta conversa.', notfound: 'Esta conversa ou mensagem já não existe.' });

/** Sistema RTUB first, then the groups, then the people; inside each the server's order (pinned, then latest activity). */
const TIER: Record<string, number> = { system: 0, group: 1, direct: 2 };

export const byTier = (list: ConversationItem[]) => [...list].sort((a, b) => TIER[a.kind] - TIER[b.kind]);

/** A chat being started in "Nova": shown like a conversation, but stored only when its first message is sent. */
export type Draft = { people: Person[]; title: string };

export const conversationFromUrl = () => Number(new URLSearchParams(location.search).get('c')) || undefined;
