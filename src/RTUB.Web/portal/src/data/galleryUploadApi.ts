// Gallery upload/edit types, the /api/gallery calls and the XHR upload with progress.
import { GalleryItem, GalleryPerson, antiforgeryToken, call, type Outcome } from './api';

export const MAX_IMAGE = 10 * 1024 * 1024;
export const MAX_VIDEO = 100 * 1024 * 1024;

export type Taggable = { id: string; name: string; fullName: string | null; avatarUrl: string };
export type EditData = { id: number; title: string; type: 'image' | 'video'; url: string | null; date: string; membersOnly: boolean; people: GalleryPerson[] };
export type Form = { title: string; date: string; membersOnly: boolean; people: GalleryPerson[]; eventId?: number };
/** The event an upload is for (from /pending-actions → Galeria → Adicionar). */
export type UploadEvent = { id: number; title: string; date: string };

export const galleryApi = {
  people: (q: string) => call<Taggable[]>('GET', `/api/gallery/people?q=${encodeURIComponent(q)}`),
  forEdit: (id: number) => call<EditData>('GET', `/api/gallery/items/${id}/edit`),
  update: (id: number, form: Form) =>
    call<GalleryItem>('PUT', `/api/gallery/items/${id}`, { title: form.title, date: form.date, membersOnly: form.membersOnly, personIds: form.people.map((p) => p.id) }),
  remove: (id: number) => call<void>('DELETE', `/api/gallery/items/${id}`),
};

export const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'forbidden'
    ? 'Só quem carregou esta media a pode alterar.'
    : o.kind === 'notfound'
      ? 'Esta media já não existe.'
      : o.kind === 'signin'
        ? 'A sessão terminou. Entra outra vez.'
        : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

type UploadOutcome = { kind: 'ok' } | { kind: 'invalid'; errors: Record<string, string> } | { kind: 'failed'; message: string };

/** POST /api/gallery as multipart, through XHR so the upload can report its progress. */
export async function upload(file: File, form: Form, onProgress: (share: number) => void, retried = false): Promise<UploadOutcome> {
  const body = new FormData();
  body.set('file', file);
  body.set('title', form.title);
  body.set('date', form.date);
  body.set('membersOnly', String(form.membersOnly));
  form.people.forEach((p) => body.append('personIds', p.id));
  if (form.eventId) body.set('eventId', String(form.eventId));
  let token: string;
  try {
    token = await antiforgeryToken(retried);
  } catch {
    return { kind: 'failed', message: problem({ kind: 'failed' }, 'carregar') };
  }

  const answer = await new Promise<{ status: number; text: string }>((resolve) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', '/api/gallery');
    xhr.setRequestHeader('X-CSRF-TOKEN', token);
    xhr.setRequestHeader('Accept', 'application/json');
    xhr.upload.onprogress = (e) => e.lengthComputable && onProgress(e.loaded / e.total);
    xhr.onload = () => resolve({ status: xhr.status, text: xhr.responseText });
    xhr.onerror = () => resolve({ status: 0, text: '' });
    xhr.send(body);
  });

  if (answer.status >= 200 && answer.status < 300) return { kind: 'ok' };
  const json = (() => {
    try {
      return JSON.parse(answer.text) as { errors?: Record<string, string[]> };
    } catch {
      return {};
    }
  })();
  if (answer.status === 400 && json.errors) {
    const errors: Record<string, string> = {};
    for (const [k, v] of Object.entries(json.errors)) errors[k.charAt(0).toLowerCase() + k.slice(1)] = v[0];
    return { kind: 'invalid', errors };
  }
  if (answer.status === 400 && !retried) return upload(file, form, onProgress, true); // a refused token: fetch a fresh one
  if (answer.status === 413) return { kind: 'failed', message: 'O ficheiro é demasiado grande.' };
  if (answer.status === 401) return { kind: 'failed', message: problem({ kind: 'signin' }, 'carregar') };
  return { kind: 'failed', message: problem({ kind: 'failed' }, 'carregar') };
}
