import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import { call, type Outcome } from './eventsApi';
import { Icon } from './icons';
import { loginTo } from './musicApi';

// /api/documentation (Endpoints/DocumentationEndpoints.cs, React track 022). Signed-in members only, Leitões refused;
// Owner manages. The server decides every rule (DocumentationService); hiding a button here is only a convenience.

type Option = { value: string; label: string };
type Doc = { name: string; extension: string; sizeBytes: number };
type Folder = { name: string; label: string; documents: Doc[] };
type Docs = {
  fiscalYears: Option[];
  fiscalYear: string;
  folders: Folder[];
  extensions: string[];
  maxFileBytes: number;
  canManage: boolean;
};
type Target = { folder: Folder; document?: Doc };

const query = (params: Record<string, string>) => `?${new URLSearchParams(params)}`;

const api = {
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

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Só o Owner gere pastas e documentos.'
      : o.kind === 'notfound'
        ? 'Este documento ou pasta já não existe.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

const fileSize = (bytes: number) =>
  bytes < 1024
    ? `${bytes} B`
    : bytes < 1024 ** 2
      ? `${(bytes / 1024).toFixed(1)} KB`
      : bytes < 1024 ** 3
        ? `${(bytes / 1024 ** 2).toFixed(1)} MB`
        : `${(bytes / 1024 ** 3).toFixed(1)} GB`;

const count = (n: number) => `${n} ${n === 1 ? 'documento' : 'documentos'}`;

/**
 * /documentation - Documentação (React track 022; was the Blazor page). Signed-in members, Leitões excepted. The
 * folders of one fiscal year (the current one by default) the member may see, the first one open; search over folder
 * and file names; download. Any member uploads into a folder; Owner creates and deletes folders and deletes documents.
 */
export default function Documentation() {
  const [docs, setDocs] = useState<Docs | 'signin' | 'forbidden' | null>();
  const [fiscalYear, setFiscalYear] = useState<string | null>(null);
  const [text, setText] = useState('');
  const [open, setOpen] = useState<Set<string>>(new Set());
  const [message, setMessage] = useState<string>();
  const [creating, setCreating] = useState(false);
  const [uploading, setUploading] = useState<Folder>();
  const [deleting, setDeleting] = useState<Target>();
  const ids = { q: useId(), year: useId() };

  useEffect(() => {
    document.title = 'Documentação · RTUB';
  }, []);

  const load = (first = false) =>
    api.list(fiscalYear).then((o) => {
      if (o.kind === 'ok' && first) setOpen(new Set(o.data.folders.slice(0, 1).map((f) => f.name)));
      setDocs(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : o.kind === 'forbidden' ? 'forbidden' : null);
    });

  useEffect(() => {
    load(true);
  }, [fiscalYear]);

  const data = docs && typeof docs === 'object' ? docs : null;
  const q = text.trim().toLocaleLowerCase('pt');
  const folders = !data
    ? []
    : q === ''
      ? data.folders
      : data.folders
          .map((f) => (f.label.toLocaleLowerCase('pt').includes(q) ? f : { ...f, documents: f.documents.filter((d) => d.name.toLocaleLowerCase('pt').includes(q)) }))
          .filter((f) => f.documents.length > 0 || f.label.toLocaleLowerCase('pt').includes(q));

  const toggle = (name: string) => {
    const next = new Set(open);
    if (!next.delete(name)) next.add(name);
    setOpen(next);
  };

  const download = async (folder: Folder, document: Doc) => {
    setMessage(undefined);
    const o = await api.file(data!.fiscalYear, folder.name, document.name);
    // Same tab, as the old page: no popup to block on phones.
    if (o.kind === 'ok') location.assign(o.data.url);
    else setMessage(problem(o, 'abrir o documento'));
  };

  return (
    <section className="page wrap events-page docs-page" aria-labelledby="docs-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="docs-title" className="page__title">
            Documentação
          </h1>
          <p className="page__lead">Regulamentos, atas e outros ficheiros da tuna, arrumados por pasta e ano letivo.</p>
        </div>
        {data?.canManage && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setCreating(true)}>
              <Icon name="plus" />
              Criar pasta
            </button>
          </div>
        )}
      </header>

      {docs === 'signin' ? (
        <div className="notice" role="status">
          <p>A documentação é da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.documentation)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : docs === 'forbidden' ? (
        <div className="notice" role="status">
          <p>A documentação ainda não está disponível para Leitões.</p>
        </div>
      ) : docs === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir a documentação agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => load()}>
            Tentar novamente
          </button>
        </div>
      ) : !data ? (
        <Loading label="A carregar a documentação…" />
      ) : (
        <>
          <div className="events-tools" role="search">
            <label className="control" htmlFor={ids.q}>
              <span className="sr-only">Procurar documento</span>
              <Icon name="search" />
              <input id={ids.q} type="search" placeholder="Procurar por documento ou pasta" value={text} onChange={(e) => setText(e.target.value)} />
            </label>
            <label className="control control--select" htmlFor={ids.year}>
              <span className="sr-only">Ano letivo</span>
              <select id={ids.year} value={data.fiscalYear} onChange={(e) => setFiscalYear(e.target.value)}>
                {!data.fiscalYears.some((y) => y.value === data.fiscalYear) && <option value={data.fiscalYear}>{data.fiscalYear} (ATUAL)</option>}
                {data.fiscalYears.map((y) => (
                  <option key={y.value} value={y.value}>
                    {y.label}
                  </option>
                ))}
              </select>
            </label>
          </div>

          {message && (
            <p className="form__banner" role="alert">
              {message}
            </p>
          )}

          {data.folders.length === 0 ? (
            <p className="note">Este ano letivo ainda não tem pastas.</p>
          ) : folders.length === 0 ? (
            <p className="note">Nenhum documento corresponde à pesquisa.</p>
          ) : (
            <ul className="doc-folders">
              {folders.map((f) => {
                const expanded = q !== '' || open.has(f.name);
                const bodyId = `doc-folder-${f.name.replace(/[^a-zA-Z0-9-]/g, '-')}`;
                return (
                  <li key={f.name} className="doc-folder">
                    <div className="doc-folder__head">
                      <button type="button" className="doc-folder__toggle" aria-expanded={expanded} aria-controls={bodyId} onClick={() => toggle(f.name)}>
                        <Icon name="folder" />
                        <strong>{f.label}</strong>
                        <small>{count(f.documents.length)}</small>
                        <Icon name="chevronDown" className={expanded ? 'doc-folder__chevron doc-folder__chevron--open' : 'doc-folder__chevron'} />
                      </button>
                      <span className="doc-folder__tools">
                        <button type="button" className="icon-btn" onClick={() => setUploading(f)}>
                          <Icon name="upload" />
                          <span className="sr-only">Carregar documento em {f.label}</span>
                        </button>
                        {data.canManage && (
                          <button type="button" className="icon-btn icon-btn--danger" onClick={() => setDeleting({ folder: f })}>
                            <Icon name="trash" />
                            <span className="sr-only">Eliminar a pasta {f.label}</span>
                          </button>
                        )}
                      </span>
                    </div>
                    {expanded && (
                      <div id={bodyId}>
                        {f.documents.length === 0 ? (
                          <p className="note doc-folder__empty">Pasta sem ficheiros, por agora.</p>
                        ) : (
                          <ul className="doc-files">
                            {f.documents.map((d) => (
                              <li key={d.name} className="doc-file">
                                <Icon name="file" />
                                <span className="doc-file__body">
                                  <span className="doc-file__name">{d.name}</span>
                                  <small>
                                    {d.extension.replace('.', '').toUpperCase()} · {fileSize(d.sizeBytes)}
                                  </small>
                                </span>
                                <button type="button" className="icon-btn" onClick={() => download(f, d)}>
                                  <Icon name="download" />
                                  <span className="sr-only">Descarregar {d.name}</span>
                                </button>
                                {data.canManage && (
                                  <button type="button" className="icon-btn icon-btn--danger" onClick={() => setDeleting({ folder: f, document: d })}>
                                    <Icon name="trash" />
                                    <span className="sr-only">Eliminar {d.name}</span>
                                  </button>
                                )}
                              </li>
                            ))}
                          </ul>
                        )}
                      </div>
                    )}
                  </li>
                );
              })}
            </ul>
          )}

          {creating && (
            <FolderDialog
              onClose={() => setCreating(false)}
              onDone={(name) => {
                // Created in the current year: show that year with the new folder open.
                if (fiscalYear !== null) setFiscalYear(null);
                else load().then(() => setOpen((o) => new Set(o).add(name)));
              }}
            />
          )}
          {uploading && <UploadDialog docs={data} folder={uploading} onClose={() => setUploading(undefined)} onDone={() => load()} />}
          {deleting && <DeleteDialog fiscalYear={data.fiscalYear} target={deleting} onClose={() => setDeleting(undefined)} onDone={() => load()} />}
        </>
      )}
    </section>
  );
}

/** "Criar pasta": always in the current fiscal year, as before. */
function FolderDialog({ onClose, onDone }: { onClose: () => void; onDone: (name: string) => void }) {
  const [name, setName] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o = await api.createFolder(name);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'criar a pasta'));
    onDone(o.data.name);
    onClose();
  };

  return (
    <Dialog
      title="Criar pasta"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy || name.trim() === ''}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Criar
          </button>
        </>
      }
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <div className={error ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={`${fid}-name`}>Nome da pasta</label>
          <input id={`${fid}-name`} type="text" maxLength={50} placeholder="Ex.: Regulamentos" value={name} onChange={(e) => setName(e.target.value)} />
          <p className="form__hint">Letras, números, espaços e hífens. A pasta fica no ano letivo atual.</p>
          {error && (
            <p className="form__error" role="alert">
              {error}
            </p>
          )}
        </div>
      </form>
    </Dialog>
  );
}

function UploadDialog({ docs, folder, onClose, onDone }: { docs: Docs; folder: Folder; onClose: () => void; onDone: () => void }) {
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();
  const types = docs.extensions.map((e) => e.replace('.', '').toUpperCase()).join(', ');

  const pick = (picked: File | undefined) => {
    setError(undefined);
    setFile(null);
    if (!picked) return;
    const extension = picked.name.includes('.') ? picked.name.slice(picked.name.lastIndexOf('.')).toLowerCase() : '';
    if (!docs.extensions.includes(extension)) return setError(`Tipo de ficheiro não permitido. Tipos permitidos: ${types}.`);
    if (picked.size > docs.maxFileBytes) return setError(`O ficheiro é muito grande (${fileSize(picked.size)}). Tamanho máximo: ${fileSize(docs.maxFileBytes)}.`);
    setFile(picked);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!file) return;
    setBusy(true);
    const o = await api.upload(docs.fiscalYear, folder.name, file);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'carregar o ficheiro'));
    onDone();
    onClose();
  };

  return (
    <Dialog
      title={`Carregar documento · ${folder.label}`}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy || !file}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            {busy ? 'A carregar…' : 'Carregar'}
          </button>
        </>
      }
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <div className={error ? 'form__field form__field--error' : 'form__field'}>
          <span className="form__label">Ficheiro</span>
          <span className="doc-pick">
            <label className="btn btn--ghost btn--sm" htmlFor={`${fid}-file`}>
              <Icon name="upload" />
              Escolher ficheiro
            </label>
            <span className="doc-pick__name">{file ? `${file.name} (${fileSize(file.size)})` : 'Sem ficheiro escolhido'}</span>
            <input
              id={`${fid}-file`}
              className="sr-only"
              type="file"
              accept={docs.extensions.join(',')}
              onChange={(e) => {
                pick(e.target.files?.[0]);
                e.target.value = '';
              }}
            />
          </span>
          <p className="form__hint">
            Tipos permitidos: {types} (máx. {fileSize(docs.maxFileBytes)}).
            {docs.canManage && ' Um ficheiro com o mesmo nome substitui o que já está na pasta.'}
          </p>
          {error && (
            <p className="form__error" role="alert">
              {error}
            </p>
          )}
        </div>
      </form>
    </Dialog>
  );
}

function DeleteDialog({ fiscalYear, target, onClose, onDone }: { fiscalYear: string; target: Target; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const { folder, document } = target;

  const remove = async () => {
    setBusy(true);
    const o = document ? await api.removeDocument(fiscalYear, folder.name, document.name) : await api.removeFolder(fiscalYear, folder.name);
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') {
      onDone();
      onClose();
    } else setError(problem(o, 'eliminar'));
  };

  return (
    <Dialog
      title={document ? 'Eliminar documento' : 'Eliminar pasta'}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={remove} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Eliminar
          </button>
        </>
      }
    >
      <p>
        Eliminar {document ? 'o documento' : 'a pasta'} <strong>{document?.name ?? folder.label}</strong>?
      </p>
      <p className="warning">
        <Icon name="warning" />
        {document ? 'Não dá para desfazer.' : 'Elimina também todos os documentos da pasta. Não dá para desfazer.'}
      </p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
