import { call, type Outcome } from './eventsApi';

// /api/owner/* (Endpoints/OwnerEndpoints.cs, task 035). Owner only, every call: a visitor gets 401 and any other member
// 403 from the server, whatever this page shows. Writes carry the X-CSRF-TOKEN header (call()).

export type OwnerPerson = { id: string; nickname: string; fullName: string; email: string | null; avatarUrl: string };

export type EmailRecipients = { subscribed: OwnerPerson[]; notSubscribed: OwnerPerson[] };
export type SubscriptionResult = { changed: number; errors: string[]; recipients: EmailRecipients };
export type EmailResult = { success: boolean; sent: number; total: number; message: string | null };

export type PushRecipients = { recipients: OwnerPerson[] };
export type PushResult = { recipients: number; sent: number; failed: number };

export type OwnerUserCard = {
  id: string;
  userName: string;
  nickname: string;
  fullName: string;
  email: string | null;
  avatarUrl: string;
  role: string;
  online: boolean;
  locked: boolean;
  lastLoginAt: string | null;
};
export type OwnerUserPage = { items: OwnerUserCard[]; total: number; page: number; pageSize: number };
export type OwnerUserDetail = {
  id: string;
  userName: string;
  nickname: string;
  fullName: string;
  email: string | null;
  avatarUrl: string;
  role: string;
  lastLoginAt: string | null;
  subscribed: boolean;
  locked: boolean;
  lockoutEnd: string | null;
  requirePasswordChange: boolean;
};

export type AuditFilter = {
  user: string;
  excludeUser: string;
  entityType: string;
  action: string;
  critical: boolean;
  q: string;
  page: number;
  pageSize: number;
};
export type AuditEntry = {
  id: number;
  timestamp: string;
  userName: string | null;
  entityType: string;
  entityId: number | null;
  entityDisplayName: string | null;
  action: string;
  isCritical: boolean;
  changes: string | null;
};
export type AuditPage = { items: AuditEntry[]; total: number; page: number; pageSize: number; criticalOnPage: number };
export type AuditFilters = { users: string[]; entityTypes: string[]; actions: string[] };
export type LoginRecord = { userId: string; userName: string; nickname: string; fullName: string; avatarUrl: string; lastLoginAt: string };

export type DbGrid = { columns: string[]; rows: string[][]; total: number; page: number; pageSize: number };

/** The audit filters as a query string (also the export link). */
export function auditQuery(f: Partial<AuditFilter>): string {
  const p = new URLSearchParams();
  if (f.user) p.set('user', f.user);
  if (f.excludeUser) p.set('excludeUser', f.excludeUser);
  if (f.entityType) p.set('entityType', f.entityType);
  if (f.action) p.set('action', f.action);
  if (f.critical) p.set('critical', 'true');
  if (f.q) p.set('q', f.q);
  if (f.page) p.set('page', String(f.page));
  if (f.pageSize) p.set('pageSize', String(f.pageSize));
  const s = p.toString();
  return s ? `?${s}` : '';
}

export const ownerApi = {
  emailRecipients: () => call<EmailRecipients>('GET', '/api/owner/emails'),
  setSubscription: (userIds: string[], subscribed: boolean) =>
    call<SubscriptionResult>('PUT', '/api/owner/emails/recipients', { userIds, subscribed }),
  sendEmail: (title: string, content: string) => call<EmailResult>('POST', '/api/owner/emails', { title, content }),

  pushRecipients: () => call<PushRecipients>('GET', '/api/owner/notifications'),
  sendPush: (title: string, body: string, userIds: string[]) => call<PushResult>('POST', '/api/owner/notifications', { title, body, userIds }),

  users: (q: string, page: number, pageSize: number) =>
    call<OwnerUserPage>('GET', `/api/owner/users?${new URLSearchParams({ q, page: String(page), pageSize: String(pageSize) })}`),
  user: (id: string) => call<OwnerUserDetail>('GET', `/api/owner/users/${encodeURIComponent(id)}`),
  changeRole: (id: string, role: string) => call<OwnerUserDetail>('PUT', `/api/owner/users/${encodeURIComponent(id)}/role`, { role }),
  unlock: (id: string) => call<OwnerUserDetail>('POST', `/api/owner/users/${encodeURIComponent(id)}/unlock`),
  syncStatus: () => call<{ updated: number; changed: number; failed: number }>('POST', '/api/owner/users/sync-status'),

  auditFilters: () => call<AuditFilters>('GET', '/api/owner/tracing/filters'),
  audit: (f: AuditFilter) => call<AuditPage>('GET', `/api/owner/tracing${auditQuery(f)}`),
  deleteAudit: (id: number) => call<void>('DELETE', `/api/owner/tracing/${id}`),
  truncateAudit: (user: string, confirmation: string) => call<void>('POST', '/api/owner/tracing/truncate', { user, confirmation }),
  logins: (from: string, to: string) => call<LoginRecord[]>('GET', `/api/owner/tracing/logins?${new URLSearchParams({ from, to })}`),
  announce: (message: string) => call<void>('POST', '/api/owner/tracing/announcement', { message }),

  tables: () => call<string[]>('GET', '/api/owner/db/tables'),
  table: (name: string, page: number, pageSize: number) =>
    call<DbGrid>('GET', `/api/owner/db/tables/${encodeURIComponent(name)}?page=${page}&pageSize=${pageSize}`),
  select: (query: string) => call<DbGrid>('POST', '/api/owner/db/select', { query }),
  modify: (query: string) => call<{ rowsAffected: number }>('POST', '/api/owner/db/modify', { query, confirmed: true }),
};

/** One sentence for a refused or failed call. */
export function ownerProblem(o: Outcome<unknown>, what: string): string {
  if (o.kind === 'invalid') return Object.values(o.errors)[0] ?? `Não foi possível ${what}.`;
  if (o.kind === 'forbidden') return 'Reservado ao Owner.';
  if (o.kind === 'signin') return 'A sessão terminou. Entra outra vez.';
  if (o.kind === 'notfound') return 'Já não existe.';
  return `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;
}

export const dateTime = (iso: string | null) =>
  iso ? new Intl.DateTimeFormat('pt-PT', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(iso)) : 'Nunca';
