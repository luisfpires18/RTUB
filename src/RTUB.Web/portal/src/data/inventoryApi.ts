// Inventory types, API client and small helpers.
import { call, type Outcome } from './eventsApi';
import { queryString, outcomeText } from './api';

// /api/inventory (Endpoints/InventoryEndpoints.cs). Signed-in members only; Mod, Admin and Owner

export type Option = { value: string; label: string };
export type Card = {
  id: number;
  name: string;
  category: string;
  categoryLabel: string;
  condition: string;
  conditionLabel: string;
  brand: string | null;
  location: string | null;
  thumbnailUrl: string | null;
};
export type Detail = Omit<Card, 'thumbnailUrl'> & {
  serialNumber: string | null;
  lastMaintenanceDate: string | null;
  maintenanceNotes: string | null;
  imageUrl: string | null;
  thumbnailUrl: string | null;
};
export type List = {
  total: number;
  stats: { condition: string; label: string; count: number }[];
  instruments: Card[];
  categories: Option[];
  conditions: Option[];
  canManage: boolean;
};
export type Input = {
  name: string;
  category: string;
  condition: string;
  brand: string;
  serialNumber: string;
  location: string;
  lastMaintenanceDate: string | null;
  maintenanceNotes: string;
};

export const api = {
  list: (q: string, category: string, condition: string) => call<List>('GET', `/api/inventory${queryString({ q, category, condition })}`),
  get: (id: number) => call<Detail>('GET', `/api/inventory/${id}`),
  create: (input: Input) => call<{ id: number }>('POST', '/api/inventory', input),
  update: (id: number, input: Input) => call<Detail>('PUT', `/api/inventory/${id}`, input),
  remove: (id: number) => call<void>('DELETE', `/api/inventory/${id}`),
  image: (id: number, image: File, thumbnail: Blob) => {
    const form = new FormData();
    form.append('image', image, image.name);
    form.append('thumbnail', thumbnail, 'instrument-thumbnail.webp');
    return call<Detail>('POST', `/api/inventory/${id}/image`, form);
  },
};

export const PAGE = 12;
export const MAX_IMAGE = 10 * 1024 * 1024;
export const IMAGE_TYPES = ['image/webp', 'image/jpeg', 'image/png'];
export const CROP_TEXT = {
  title: 'Recortar a miniatura',
  label: 'Pré-visualização da miniatura quadrada. Arraste ou use as setas para enquadrar.',
  note: 'A imagem fica inteira nos detalhes; o recorte quadrado é a miniatura da lista.',
};

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, { forbidden: 'Não tem permissão para gerir os instrumentos.', notfound: 'Este instrumento já não existe.' });

export const conditionClass = (c: string) => `inventory-condition inventory-condition--${c.toLowerCase()}`;
