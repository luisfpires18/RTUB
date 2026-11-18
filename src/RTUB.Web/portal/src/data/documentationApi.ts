// Documentation types, API client and small helpers.
import { call, type Outcome, outcomeText } from './api';
import type { Option } from './sharedTypes';

// /api/documentation (Endpoints/DocumentationEndpoints.cs). Signed-in members only, Leitões refused;
// Owner manages. The server decides every rule (DocumentationService); hiding a button here is only a convenience.

export type Doc = { name: string; extension: string; sizeBytes: number };
export type Folder = { name: string; label: string; documents: Doc[] };
export type Docs = {
  fiscalYears: Option[];
  fiscalYear: string;
  folders: Folder[];
  extensions: string[];
  maxFileBytes: number;
  canManage: boolean;
};
export type Target = { folder: Folder; document?: Doc };

const query = (params: Record<string, string>) => `?${new URLSearchParams(params)}`;

export const api = {
  // fiscalYear: null = the server's default (this year).
  list: (fiscalYear: string | null) => call<Docs>('GET', `/api/documentation${fiscalYear ? query({ fiscalYear }) : ''}`),
  file: (fiscalYear: string, folder: string, name: string) =>
    call<{ url: string }>('GET', `/api/documentation/file${query({ fiscalYear, folder, name })}`),
  createFolder: (name: string) => call<Folder>('POST', '/api/documentation/folders', { name }),
  removeFolder: (fiscalYear: string, folder: string) => call<void>('DELETE', `/api/documentation/folders${query({ fiscalYear, folder })}`),
  upload: (fiscalYear: string, folder: string, file: File) => {
    const form = new FormData();
    form.append('fiscalYear', fiscalYear);
    form.append('folder', folder);
    form.append('file', file, file.name);
    return call<Doc>('POST', '/api/documentation/documents', form);
  },
  removeDocument: (fiscalYear: string, folder: string, name: string) =>
    call<void>('DELETE', `/api/documentation/documents${query({ fiscalYear, folder, name })}`),
};

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, { forbidden: 'Não tem permissão para gerir pastas e documentos.', notfound: 'Este documento ou pasta já não existe.' });

export const fileSize = (bytes: number) =>
  bytes < 1024
    ? `${bytes} B`
    : bytes < 1024 ** 2
      ? `${(bytes / 1024).toFixed(1)} KB`
      : bytes < 1024 ** 3
        ? `${(bytes / 1024 ** 2).toFixed(1)} MB`
        : `${(bytes / 1024 ** 3).toFixed(1)} GB`;

export const count = (n: number) => `${n} ${n === 1 ? 'documento' : 'documentos'}`;
