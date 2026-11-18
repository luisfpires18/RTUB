// Shop types, API client and small helpers.
import { call, type Outcome, outcomeText } from './api';
import type { Option } from './sharedTypes';

// /api/shop: visitors receive a public-only catalogue; members retain their existing actions.

export type Reservation = { id: number; username: string; displayName: string | null; size: string | null; createdAt: string };
export type Product = {
  id: number;
  name: string;
  type: string;
  price: number;
  stock: number;
  isPublic: boolean;
  imageUrl: string | null;
  myReservation: Reservation | null;
  canReserve: boolean;
  availableAllYears: boolean;
  /** Admin / Owner only: the reservations a delete removes; null for everyone else. */
  reservationCount: number | null;
};
type PublicProduct = {
  id: number;
  name: string;
  type: string;
  price: number;
  stock: number;
  imageUrl: string | null;
  description: string | null;
  availableAllYears: boolean;
};
export type Detail = {
  id: number;
  name: string;
  type: string;
  price: number;
  stock: number;
  isAvailable: boolean;
  isPublic: boolean;
  description: string | null;
  imageUrl: string | null;
  availableAllYears: boolean;
  /** The fiscal year the product was created in: the only one a year-specific product is shown in. */
  fiscalYear: string;
};
type Shop = {
  fiscalYears: Option[];
  fiscalYear: string;
  types: string[];
  products: Product[];
  sizes: string[];
  canCreate: boolean;
  canManage: boolean;
  canManageReservations: boolean;
};
type PublicShop = { fiscalYears: Option[]; fiscalYear: string; types: string[]; products: PublicProduct[] };
export type Catalogue = Shop | PublicShop;
export type ProductInput = {
  name: string;
  type: string;
  price: number;
  stock: number;
  isPublic: boolean;
  description: string;
  availableAllYears: boolean;
};

export const SIZE_LABELS: Record<string, string> = {
  XS: 'XS - Extra Small',
  S: 'S - Small',
  M: 'M - Medium',
  L: 'L - Large',
  XL: 'XL - Extra Large',
  XXL: 'XXL - 2X Large',
  XXXL: 'XXXL - 3X Large',
};

export const api = {
  // fiscalYear: null = the server's default (this year); '' = every year.
  shop: (fiscalYear: string | null, q: string, type: string) => {
    const p = new URLSearchParams();
    if (fiscalYear !== null) p.set('fiscalYear', fiscalYear);
    if (q) p.set('q', q);
    if (type) p.set('type', type);
    const s = p.toString();
    return call<Catalogue>('GET', `/api/shop${s ? `?${s}` : ''}`);
  },
  product: (id: number) => call<Detail>('GET', `/api/shop/products/${id}`),
  create: (input: ProductInput) => call<{ id: number }>('POST', '/api/shop/products', input),
  update: (id: number, input: ProductInput) => call<Detail>('PUT', `/api/shop/products/${id}`, input),
  remove: (id: number) => call<void>('DELETE', `/api/shop/products/${id}`),
  image: (id: number, image: Blob) => {
    const form = new FormData();
    form.append('image', image, 'product-image.webp');
    return call<Detail>('POST', `/api/shop/products/${id}/image`, form);
  },
  reserve: (id: number, body: { hasSizes: boolean; size: string | null; displayName: string | null }) =>
    call<Reservation>('POST', `/api/shop/products/${id}/reservations`, body),
  reservations: (id: number) => call<Reservation[]>('GET', `/api/shop/products/${id}/reservations`),
  cancel: (id: number) => call<void>('DELETE', `/api/shop/reservations/${id}`),
};

export const PAGE = 12;
export const MAX_SOURCE = 10 * 1024 * 1024;
export const CROP_TEXT = {
  title: 'Recortar a imagem',
  label: 'Pré-visualização da imagem quadrada do produto. Arraste ou use as setas para enquadrar.',
  note: 'Os produtos aparecem sempre em quadrado.',
};

export const when = (iso: string) => {
  const d = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`);
  return `${d.toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', year: 'numeric' })} ${d.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' })}`;
};

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, { forbidden: 'Não tem permissão para esta ação.', notfound: 'Já não existe.' });
