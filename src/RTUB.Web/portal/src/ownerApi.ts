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
  /** Hide what the application wrote by itself (status reconciliation, background jobs, no signed-in member). */
  hideSystem: boolean;
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
  /** #220, never stored: today's name for an old entry whose stored name is missing or carries a member id. */
  currentDisplayName: string | null;
};
export type AuditPage = { items: AuditEntry[]; total: number; page: number; pageSize: number; criticalOnPage: number };
export type AuditFilters = { users: string[]; entityTypes: string[]; actions: string[] };
/**
 * Task 050: from LoginEvents. lastLoginAt / firstAt are the latest / earliest sign-in or activity in the range;
 * fromHistory false = listed only by the account's last access (no LoginEvents row in the range).
 */
export type LoginRecord = {
  userId: string;
  userName: string;
  nickname: string;
  fullName: string;
  avatarUrl: string;
  lastLoginAt: string;
  signIns: number;
  activeDays: number;
  firstAt: string;
  signInTimes: string[];
  fromHistory: boolean;
};
export type Logins = { records: LoginRecord[]; historySince: string | null };

export type DbGrid = { columns: string[]; rows: string[][]; total: number; page: number; pageSize: number };

/** The audit filters as a query string (also the export link). */
export function auditQuery(f: Partial<AuditFilter>): string {
  const p = new URLSearchParams();
  if (f.user) p.set('user', f.user);
  if (f.excludeUser) p.set('excludeUser', f.excludeUser);
  if (f.entityType) p.set('entityType', f.entityType);
  if (f.action) p.set('action', f.action);
  if (f.critical) p.set('critical', 'true');
  if (f.hideSystem) p.set('system', 'hide');
  if (f.q) p.set('q', f.q);
  if (f.page) p.set('page', String(f.page));
  if (f.pageSize) p.set('pageSize', String(f.pageSize));
  const s = p.toString();
  return s ? `?${s}` : '';
}

/**
 * #220: the words the page shows for a stored EntityType. Presentation only (/owner/tracing and /owner/dashboard): filters, export and Details keep the raw
 * type; an unknown type shows as it is.
 */
export const ENTITY_LABELS: Record<string, string> = {
  ApplicationUser: 'Utilizador',
  UserRole: 'Função de acesso',
  RoleAssignment: 'Cargo',
  PushSubscription: 'Subscrição push',
  RehearsalAttendance: 'Presença em ensaio',
  Rehearsal: 'Ensaio',
  Enrollment: 'Inscrição em atuação',
  Event: 'Atuação',
  EventRepertoire: 'Repertório',
  EventVideo: 'Vídeo de atuação',
  EventContact: 'Contacto de atuação',
  Song: 'Música',
  SongVideo: 'Vídeo de música',
  SongYouTubeUrl: 'Link YouTube',
  Album: 'Álbum',
  AlbumAccess: 'Acesso a álbum',
  NewsPost: 'Novidade',
  PostMedia: 'Anexo de publicação',
  CommentImage: 'Imagem de comentário',
  Meeting: 'Reunião',
  MeetingRequest: 'Pedido de reunião',
  MeetingParticipation: 'Presença em reunião',
  MeetingAta: 'Ata',
  MeetingAtaAttachment: 'Anexo de ata',
  Product: 'Produto',
  ProductReservation: 'Reserva de produto',
  ShopPromotion: 'Promoção da loja',
  ShopPromotionItem: 'Produto de promoção',
  Transaction: 'Movimento',
  Report: 'Relatório',
  Activity: 'Atividade',
  MemberDebt: 'Calote',
  MbwayTransfer: 'Transferência MBWay',
  NerbaOrder: 'Encomenda Nerba',
  Request: 'Pedido de atuação',
  Question: 'Pergunta',
  QuestionReply: 'Resposta',
  LogisticsBoard: 'Quadro',
  LogisticsList: 'Lista',
  LogisticsCard: 'Cartão',
  LogisticsCardAssignment: 'Atribuição de cartão',
  LogisticsCardReminder: 'Lembrete',
  Post: 'Publicação',
  Comment: 'Comentário',
  Discussion: 'Discussão',
  Transportation: 'Boleia',
  TransportationPassenger: 'Passageiro',
  LeaderboardComment: 'Comentário na classificação',
  LeaderboardCommentLike: 'Gosto em comentário',
  MemberInstrument: 'Instrumento do membro',
  Instrument: 'Instrumento da tuna',
  Trophy: 'Troféu',
  GalleryMedia: 'Média da galeria',
  NaipeContent: 'Conteúdo de naipe',
  NaipeComment: 'Comentário de naipe',
  NaipeTypeConfig: 'Configuração de naipe',
  FiscalYear: 'Ano letivo',
  Document: 'Documento',
  Folder: 'Pasta',
  Notification: 'Notificação',
  MemberActivityStatus: 'Estado de atividade',
  GeocodingCache: 'Cache de localização',
};

export const entityLabel = (type: string) => ENTITY_LABELS[type] ?? type;

/** /owner/dashboard (task 050): one value per Portugal day; see OwnerOperationsService.Dashboard.cs. */
export type DashboardSeries = { key: string; label: string; values: number[]; total: number; previousTotal: number };
export type DashboardRank = { label: string; count: number };
export type Dashboard = {
  from: string;
  to: string;
  days: string[];
  series: DashboardSeries[];
  topSongs: DashboardRank[];
  topNaipes: DashboardRank[];
  topMembers: DashboardRank[];
  areas: DashboardRank[];
  actions: DashboardRank[];
  /** 7 rows (Monday first) × 24 Portugal hours. */
  hours: number[][];
  now: { accounts: number; online: number; pushMembers: number };
};

export const ownerApi = {
  emailRecipients: () => call<EmailRecipients>('GET', '/api/owner/emails'),
  setSubscription: (userIds: string[], subscribed: boolean) =>
    call<SubscriptionResult>('PUT', '/api/owner/emails/recipients', { userIds, subscribed }),
  sendEmail: (title: string, content: string) => call<EmailResult>('POST', '/api/owner/emails', { title, content }),

  pushRecipients: () => call<PushRecipients>('GET', '/api/owner/notifications'),
  sendPush: (title: string, body: string, userIds: string[]) => call<PushResult>('POST', '/api/owner/notifications', { title, body, userIds }),

  users: (q: string, page: number, pageSize: number, staff = false) =>
    call<OwnerUserPage>('GET', `/api/owner/users?${new URLSearchParams({ q, page: String(page), pageSize: String(pageSize), ...(staff && { staff: 'true' }) })}`),
  user: (id: string) => call<OwnerUserDetail>('GET', `/api/owner/users/${encodeURIComponent(id)}`),
  changeRole: (id: string, role: string) => call<OwnerUserDetail>('PUT', `/api/owner/users/${encodeURIComponent(id)}/role`, { role }),
  unlock: (id: string) => call<OwnerUserDetail>('POST', `/api/owner/users/${encodeURIComponent(id)}/unlock`),
  syncStatus: () => call<{ updated: number; changed: number; failed: number }>('POST', '/api/owner/users/sync-status'),

  auditFilters: () => call<AuditFilters>('GET', '/api/owner/tracing/filters'),
  audit: (f: AuditFilter) => call<AuditPage>('GET', `/api/owner/tracing${auditQuery(f)}`),
  deleteAudit: (id: number) => call<void>('DELETE', `/api/owner/tracing/${id}`),
  truncateAudit: (user: string, confirmation: string) => call<void>('POST', '/api/owner/tracing/truncate', { user, confirmation }),
  logins: (from: string, to: string) => call<Logins>('GET', `/api/owner/tracing/logins?${new URLSearchParams({ from, to })}`),
  dashboard: (from: string, to: string) => call<Dashboard>('GET', `/api/owner/dashboard?${new URLSearchParams({ from, to })}`),
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
