// Types, API calls and display helpers for the /requests page.
import { call, type Outcome, outcomeText } from './api';
import type { Option } from './sharedTypes';

// /api/requests (Endpoints/RequestAdminEndpoints.cs). Dates are yyyy-MM-dd; createdAt is UTC.
type Status = 'pending' | 'analysing' | 'confirmed' | 'rejected';

export type RequestItem = {
  id: number;
  name: string;
  email: string;
  phone: string;
  eventType: string;
  preferredDate: string;
  preferredEndDate: string | null;
  location: string;
  message: string;
  status: Status;
  createdAt: string;
};

export type RequestList = { fiscalYears: Option[]; fiscalYear: string; pending: RequestItem[]; answered: RequestItem[]; canManage: boolean };

export const api = {
  // fiscalYear: null = the server's default (the current year), "" = every year.
  list: (fiscalYear: string | null, q: string, status: string) => {
    const p = new URLSearchParams();
    if (fiscalYear !== null) p.set('fiscalYear', fiscalYear);
    if (q) p.set('q', q);
    if (status) p.set('status', status);
    const s = p.toString();
    return call<RequestList>('GET', `/api/requests${s ? `?${s}` : ''}`);
  },
  approve: (id: number) => call<{ request: RequestItem; createEventUrl: string }>('POST', `/api/requests/${id}/approve`),
  reject: (id: number) => call<RequestItem>('POST', `/api/requests/${id}/reject`),
  remove: (id: number) => call<void>('DELETE', `/api/requests/${id}`),
};

export const PAGE = 8;

export const statusLabel: Record<Status, string> = {
  pending: 'Pendente',
  analysing: 'Em análise',
  confirmed: 'Aprovado',
  rejected: 'Rejeitado',
};

export const statusPill: Record<Status, string> = { pending: 'pill pill--wait', analysing: 'pill pill--wait', confirmed: 'pill pill--yes', rejected: 'pill pill--no' };

const day = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short', year: 'numeric' });

export const when = (r: RequestItem) => (r.preferredEndDate ? `${day(r.preferredDate)} – ${day(r.preferredEndDate)}` : day(r.preferredDate));

export const submitted = (iso: string) =>
  new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`).toLocaleString('pt-PT', { dateStyle: 'medium', timeStyle: 'short' });

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, { forbidden: 'Não tem permissão para esta ação.', notfound: 'Este pedido já não existe.' });
