import { useEffect, useId, useState } from 'react';
import { Loading } from '../components/AppShared';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { Notice, RetryNotice, SignInNotice } from '../components/Notice';
import { type Doc, type Folder, type Docs, type Target, api, problem, fileSize, count } from '../data/documentationApi';
import { FolderDialog, UploadDialog, DeleteDialog } from '../components/DocumentationDialogs';

/**
 * /documentation - Documentação. Signed-in members, Leitões excepted. The
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
    // Same tab: no popup to block on phones.
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
        <SignInNotice path={portal.documentation}>A documentação é da área de membros.</SignInNotice>
      ) : docs === 'forbidden' ? (
        <Notice>A documentação ainda não está disponível para Leitões.</Notice>
      ) : docs === null ? (
        <RetryNotice onRetry={() => load()}>Não conseguimos abrir a documentação agora.</RetryNotice>
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
