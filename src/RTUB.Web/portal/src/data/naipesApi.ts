import { call } from './eventsApi';
import type { Option } from './membersApi';

// /api/naipes (Endpoints/NaipeEndpoints.cs, task 033). Signed-in members only; the settings are Admin / Owner. The
// server decides every rule (NaipeBoardService); hiding a button here is only a convenience.

export type NaipeInstrument = { value: string; label: string; pictureUrl: string | null };
export type NaipeItem = {
  id: number;
  instrument: string;
  instrumentLabel: string;
  title: string;
  description: string | null;
  url: string;
  mimeType: string;
  isVideo: boolean;
  sortOrder: number;
  createdBy: string;
  createdAt: string;
  playCount: number;
  commentCount: number;
  canEdit: boolean;
};
export type NaipeBoard = {
  instruments: NaipeInstrument[];
  instrument: string | null;
  items: NaipeItem[];
  totalForInstrument: number;
  instrumentOptions: Option[];
  nextSortOrder: number;
  canConfigure: boolean;
};
export type NaipeComment = { id: number; authorName: string; authorAvatarUrl: string; text: string; createdAt: string; canDelete: boolean };
export type NaipeSetting = { id: number; instrument: string; label: string; pictureUrl: string | null; isVisible: boolean; sortOrder: number };
export type NaipeUploadForm = { file: File; instrument: string; isVideo: boolean; title: string; description: string; sortOrder: string };

const query = (instrument: string, q: string) => {
  const p = new URLSearchParams();
  if (instrument) p.set('instrument', instrument);
  if (q) p.set('q', q);
  const s = p.toString();
  return s ? `?${s}` : '';
};

export const naipesApi = {
  board: (instrument: string, q: string) => call<NaipeBoard>('GET', `/api/naipes${query(instrument, q)}`),
  create: (f: NaipeUploadForm) => {
    const form = new FormData();
    form.set('file', f.file);
    form.set('instrument', f.instrument);
    form.set('kind', f.isVideo ? 'video' : 'image');
    form.set('title', f.title);
    form.set('description', f.description);
    form.set('sortOrder', f.sortOrder);
    return call<NaipeItem>('POST', '/api/naipes', form);
  },
  update: (id: number, title: string, description: string, sortOrder: number | null) =>
    call<NaipeItem>('PUT', `/api/naipes/${id}`, { title, description, sortOrder }),
  remove: (id: number) => call<void>('DELETE', `/api/naipes/${id}`),
  played: (id: number) => call<void>('POST', `/api/naipes/${id}/plays`),
  comments: (id: number) => call<NaipeComment[]>('GET', `/api/naipes/${id}/comments`),
  addComment: (id: number, text: string) => call<NaipeComment[]>('POST', `/api/naipes/${id}/comments`, { text }),
  removeComment: (id: number, commentId: number) => call<NaipeComment[]>('DELETE', `/api/naipes/${id}/comments/${commentId}`),
  settings: () => call<NaipeSetting[]>('GET', '/api/naipes/config'),
  saveSetting: (id: number, isVisible: boolean, sortOrder: number | null) =>
    call<NaipeSetting[]>('PUT', `/api/naipes/config/${id}`, { isVisible, sortOrder }),
  setPicture: (id: number, picture: File) => {
    const form = new FormData();
    form.set('picture', picture);
    return call<NaipeSetting[]>('POST', `/api/naipes/config/${id}/picture`, form);
  },
  removePicture: (id: number) => call<NaipeSetting[]>('DELETE', `/api/naipes/config/${id}/picture`),
};
