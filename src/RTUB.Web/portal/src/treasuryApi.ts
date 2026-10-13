import { call, type Outcome } from './eventsApi';

// /api/treasury (Endpoints/TreasuryEndpoints.cs, React track 024). Signed-in members; the server decides every rule
// (TreasuryAuthorization): treasury members read, Caloiros and Leitões see only their own calotes.

export type Person = { id: string | null; displayName: string; fullName: string | null; avatarUrl: string | null };

export type ReportSummary = {
  id: number;
  title: string;
  year: number;
  isPublished: boolean;
  publishedAt: string | null;
  isCurrentYear: boolean;
  totalIncome: number;
  totalExpenses: number;
  balance: number;
};
export type Reports = { reports: ReportSummary[]; canCreate: boolean; canPublish: boolean; availableYears: number[] };

export type Transaction = { id: number; date: string; description: string; category: string; amount: number; type: 'Income' | 'Expense'; receiptUrl: string | null };
export type Activity = {
  id: number;
  name: string;
  description: string | null;
  startDate: string;
  endDate: string | null;
  isLocked: boolean;
  income: number;
  expenses: number;
  balance: number;
  transactions: Transaction[];
};
export type Totals = { totalMoney: number; bank: number; cash: number; calotes: number; income: number; expenses: number; balance: number };
export type Report = {
  id: number;
  title: string;
  year: number;
  summary: string | null;
  isPublished: boolean;
  totals: Totals;
  activities: Activity[];
  canManage: boolean;
  canPublish: boolean;
  canSeeHistory: boolean;
};
export type ActivityInput = { name: string; startDate: string; endDate: string | null; description: string };
export type TransactionInput = { date: string; description: string; category: string; amount: string; type: string; removeReceipt: boolean; receipt: File | null };
export type History = {
  entries: { timestamp: string; activityName: string; description: string; userName: string; action: string }[];
  total: number;
  page: number;
  pageSize: number;
};

export type DebtGroup = { member: Person; total: number; compromisedUntil: string | null; debts: { id: number; amount: number; description: string | null }[] };
export type Calotes = { fiscalYears: string[]; fiscalYear: string | null; scope: 'all' | 'own'; total: number; groups: DebtGroup[]; canManage: boolean };

export type Transfer = {
  id: number;
  date: string;
  amount: number;
  transferTo: string;
  transferFrom: string | null;
  phone: string | null;
  description: string | null;
  createdBy: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  member: Person | null;
};
export type Transfers = { transfers: Transfer[]; totals: { name: string; amount: number }[]; canAdd: boolean; canEdit: boolean };
export type TransferInput = { date: string; amount: number | null; memberUserId: string | null; transferFrom: string; phone: string; description: string };

export type NerbaEvent = { id: number; name: string; date: string; endDate: string | null; location: string | null };
export type NerbaSummary = { event: NerbaEvent; count: number; total: number };
export type Nerba = { upcoming: NerbaSummary[]; past: NerbaSummary[]; canManage: boolean };
export type NerbaOrder = { id: number; item: string; type: string | null; stock: number; pricePerUnit: number; total: number; orderDate: string | null };
export type NerbaOrders = { event: NerbaEvent; days: string[]; orders: NerbaOrder[]; canManage: boolean };
export type NerbaOrderInput = { item: string; type: string; stock: number | null; pricePerUnit: number | null; orderDate: string | null };

const transactionForm = (input: TransactionInput) => {
  const form = new FormData();
  form.append('date', input.date);
  form.append('description', input.description);
  form.append('category', input.category);
  form.append('amount', input.amount.replace(',', '.'));
  form.append('type', input.type);
  form.append('removeReceipt', String(input.removeReceipt));
  if (input.receipt) form.append('receipt', input.receipt, input.receipt.name);
  return form;
};

export const treasuryApi = {
  reports: () => call<Reports>('GET', '/api/treasury'),
  createReport: (year: number) => call<ReportSummary>('POST', '/api/treasury/reports', { year }),
  publish: (id: number) => call<ReportSummary>('POST', `/api/treasury/reports/${id}/publish`),
  removeReport: (id: number) => call<void>('DELETE', `/api/treasury/reports/${id}`),

  report: (id: number) => call<Report>('GET', `/api/treasury/reports/${id}`),
  history: (id: number, page: number, pageSize: number) => call<History>('GET', `/api/treasury/reports/${id}/history?page=${page}&pageSize=${pageSize}`),
  setBalance: (id: number, kind: 'bank' | 'cash', value: number) => call<Report>('PUT', `/api/treasury/reports/${id}/balance`, { kind, value }),
  createActivity: (reportId: number, input: ActivityInput) => call<Report>('POST', `/api/treasury/reports/${reportId}/activities`, input),
  updateActivity: (id: number, input: ActivityInput) => call<Report>('PUT', `/api/treasury/activities/${id}`, input),
  lockActivity: (id: number, locked: boolean) => call<Report>('POST', `/api/treasury/activities/${id}/lock`, { locked }),
  removeActivity: (id: number) => call<Report>('DELETE', `/api/treasury/activities/${id}`),
  createTransaction: (activityId: number, input: TransactionInput) =>
    call<Report>('POST', `/api/treasury/activities/${activityId}/transactions`, transactionForm(input)),
  updateTransaction: (id: number, input: TransactionInput) => call<Report>('PUT', `/api/treasury/transactions/${id}`, transactionForm(input)),
  removeTransaction: (id: number) => call<Report>('DELETE', `/api/treasury/transactions/${id}`),

  calotes: (fy: string) => call<Calotes>('GET', `/api/treasury/calotes${fy ? `?fy=${encodeURIComponent(fy)}` : ''}`),
  addDebt: (input: { fiscalYear: string; userId: string; amount: number | null; description: string }) => call<Calotes>('POST', '/api/treasury/calotes', input),
  updateDebt: (id: number, input: { amount: number | null; description: string }) => call<Calotes>('PUT', `/api/treasury/calotes/${id}`, input),
  removeDebt: (id: number) => call<Calotes>('DELETE', `/api/treasury/calotes/${id}`),
  setCommitment: (fiscalYear: string, userId: string, until: string | null) =>
    call<Calotes>('PUT', '/api/treasury/calotes/commitment', { fiscalYear, userId, until }),
  members: (q: string, forDebts: boolean) =>
    call<Person[]>('GET', `/api/treasury/members?q=${encodeURIComponent(q)}${forDebts ? '&forDebts=true' : ''}`),

  transfers: () => call<Transfers>('GET', '/api/treasury/mbway'),
  addTransfer: (input: TransferInput) => call<Transfers>('POST', '/api/treasury/mbway', input),
  updateTransfer: (id: number, input: TransferInput) => call<Transfers>('PUT', `/api/treasury/mbway/${id}`, input),
  removeTransfer: (id: number) => call<Transfers>('DELETE', `/api/treasury/mbway/${id}`),

  nerba: () => call<Nerba>('GET', '/api/treasury/nerba'),
  removeNerbaOrders: (eventId: number) => call<Nerba>('DELETE', `/api/treasury/nerba/${eventId}`),
  nerbaOrders: (eventId: number) => call<NerbaOrders>('GET', `/api/treasury/nerba/${eventId}`),
  addNerbaOrder: (eventId: number, input: NerbaOrderInput) => call<NerbaOrders>('POST', `/api/treasury/nerba/${eventId}/orders`, input),
  updateNerbaOrder: (id: number, input: NerbaOrderInput) => call<NerbaOrders>('PUT', `/api/treasury/nerba/orders/${id}`, input),
  removeNerbaOrder: (id: number) => call<NerbaOrders>('DELETE', `/api/treasury/nerba/orders/${id}`),
};

/** 1234.5 → "1234,50 €" (pt-PT, two decimals), as the old pages showed money. */
export const euros = (n: number) => `${n.toLocaleString('pt-PT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} €`;

/** "2026-10-04T00:00:00" → "04/10/2026". */
export const day = (iso: string | null) => (iso ? iso.slice(0, 10).split('-').reverse().join('/') : '');

/** "2026-10-04T00:00:00" → "2026-10-04" (a date input's value). */
export const isoDay = (iso: string | null) => (iso ? iso.slice(0, 10) : '');

/** Today as a date input's value (local time). */
export const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

/** Words of a query all found in one of the texts (accents and case ignored), as the old multi-word search. */
export const matches = (query: string, ...texts: (string | null | undefined)[]) => {
  const fold = (s: string) => s.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();
  const hay = fold(texts.filter(Boolean).join(' '));
  return fold(query).split(/\s+/).filter(Boolean).every((w) => hay.includes(w));
};

export const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Não tens permissão para esta ação.'
      : o.kind === 'closed'
        ? 'O relatório já foi publicado ou a atividade está bloqueada. Recarrega a página.'
        : o.kind === 'notfound'
          ? 'Isto já não existe. Recarrega a página.'
          : o.kind === 'signin'
            ? 'A sessão terminou. Entra outra vez.'
            : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;
