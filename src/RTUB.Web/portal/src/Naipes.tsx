import { useEffect, useId, useRef, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import type { Outcome } from './eventsApi';
import { Icon } from './icons';
import { MemberFace } from './MemberDialogs';
import { loginTo } from './musicApi';
import { naipesApi, type NaipeBoard, type NaipeComment, type NaipeItem } from './naipesApi';

const PAGE_SIZES = [10, 20, 30, 40, 50];
const MAX_VIDEO = 100 * 1024 * 1024;
const MAX_IMAGE = 10 * 1024 * 1024;
const MAX_COMMENT = 1000;
// The formats NaipeBoardService accepts (it checks the bytes too).
const VIDEO_ACCEPT = 'video/mp4,video/quicktime,video/webm,video/3gpp,.mp4,.m4v,.mov,.webm,.3gp';
const IMAGE_ACCEPT = 'image/jpeg,image/png,image/webp,image/gif';

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Só quem adicionou este conteúdo, um Admin ou o Owner o pode alterar.'
      : o.kind === 'notfound'
        ? 'Este conteúdo já não existe.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

type Editing = { kind: 'new'; isVideo: boolean } | { kind: 'edit'; item: NaipeItem };

/**
 * /naipes (task 033; was the Blazor page). Each instrument's teaching videos and images: pick an instrument, search,
 * open one to watch it and comment. Any member adds content and edits or deletes what they added; Admin and Owner
 * manage everything and the instrument settings (/naipes/config). The server decides every rule (NaipeBoardService).
 */
export default function Naipes() {
  const initial = new URLSearchParams(location.search);
  const [instrument, setInstrument] = useState(initial.get('instrument') ?? '');
  const [text, setText] = useState(initial.get('q') ?? '');
  const [q, setQ] = useState(text);
  const [board, setBoard] = useState<NaipeBoard | 'signin' | null>();
  const [version, setVersion] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [open, setOpen] = useState<NaipeItem>();
  const [editing, setEditing] = useState<Editing>();
  const [deleting, setDeleting] = useState<NaipeItem>();
  const reveal = useRef<number>(undefined); // after a save: show the page that holds that item (a new one goes last)

  useEffect(() => {
    document.title = 'Naipes · RTUB';
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  useEffect(() => {
    const url = new URL(location.href);
    instrument ? url.searchParams.set('instrument', instrument) : url.searchParams.delete('instrument');
    q ? url.searchParams.set('q', q) : url.searchParams.delete('q');
    history.replaceState(null, '', url.pathname + url.search);
    naipesApi.board(instrument, q).then((o) => {
      if (o.kind === 'ok' && reveal.current !== undefined) {
        const index = o.data.items.findIndex((i) => i.id === reveal.current);
        if (index >= 0) setPage(Math.floor(index / pageSize) + 1);
        reveal.current = undefined;
      }
      setBoard(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null);
    });
  }, [instrument, q, version]);

  const data = board && board !== 'signin' ? board : null;
  const reload = () => setVersion((v) => v + 1);
  const pick = (value: string) => {
    setInstrument(value);
    setPage(1);
  };
  const pages = data ? Math.max(1, Math.ceil(data.items.length / pageSize)) : 1;
  const shown = data ? data.items.slice((Math.min(page, pages) - 1) * pageSize, Math.min(page, pages) * pageSize) : [];
  const chosen = data?.instruments.find((i) => i.value === data.instrument);

  return (
    <section className="page wrap events-page naipes-page" aria-labelledby="naipes-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="naipes-title" className="page__title">
            Naipes
          </h1>
          <p className="page__lead">Vídeos e imagens para estudar cada instrumento, partilhados pelos membros.</p>
        </div>
        {data && (
          <div className="events-page__actions">
            {data.canConfigure && (
              <a className="btn btn--ghost btn--sm" href={portal.naipesConfig}>
                <Icon name="pencil" />
                Configurar
              </a>
            )}
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setEditing({ kind: 'new', isVideo: true })}>
              <Icon name="video" />
              Adicionar vídeo
            </button>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setEditing({ kind: 'new', isVideo: false })}>
              <Icon name="images" />
              Adicionar imagem
            </button>
          </div>
        )}
      </header>

      {board === undefined ? (
        <Loading label="A carregar os naipes…" />
      ) : board === 'signin' ? (
        <div className="notice" role="status">
          <p>Os naipes são da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.naipes)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : board === null ? (
        <div className="notice" role="status">
          <p>Não foi possível carregar os naipes. Tenta outra vez daqui a pouco.</p>
        </div>
      ) : (
        <>
          {board.instruments.length === 0 ? (
            <p className="notice">Ainda não há instrumentos visíveis. Um Admin ou o Owner escolhe-os em Configurar.</p>
          ) : (
            <ul className="naipe-picker" aria-label="Instrumentos">
              {board.instruments.map((i) => (
                <li key={i.value}>
                  <button
                    type="button"
                    className={i.value === board.instrument ? 'naipe-pick is-active' : 'naipe-pick'}
                    aria-pressed={i.value === board.instrument}
                    onClick={() => pick(i.value)}
                  >
                    {i.pictureUrl ? <img src={i.pictureUrl} alt="" width="56" height="56" loading="lazy" /> : <Icon name="music" />}
                    <span>{i.label}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}

          {!board.instrument ? (
            <p className="notice naipe-empty">Escolhe um instrumento para veres os vídeos e as imagens dele.</p>
          ) : board.totalForInstrument === 0 ? (
            <p className="notice naipe-empty">Ainda não há vídeos nem imagens de {chosen?.label ?? 'este instrumento'}. Podes ser o primeiro a juntar.</p>
          ) : (
            <>
              <label className="control naipe-search">
                <Icon name="search" />
                <span className="sr-only">Procurar</span>
                <input type="search" placeholder="Procurar no título ou na descrição" value={text} onChange={(e) => (setText(e.target.value), setPage(1))} />
              </label>
              {board.items.length === 0 ? (
                <p className="notice naipe-empty">Nada encontrado para “{q}”.</p>
              ) : (
                <>
                  <ul className="naipe-grid">
                    {shown.map((item) => (
                      <li key={item.id}>
                        <NaipeCard item={item} onOpen={() => setOpen(item)} onEdit={() => setEditing({ kind: 'edit', item })} onDelete={() => setDeleting(item)} />
                      </li>
                    ))}
                  </ul>
                  <Pager
                    total={board.items.length}
                    page={Math.min(page, pages)}
                    pages={pages}
                    pageSize={pageSize}
                    onPage={setPage}
                    onPageSize={(size) => (setPageSize(size), setPage(1))}
                  />
                </>
              )}
            </>
          )}
        </>
      )}

      {open && <ViewDialog item={open} onClose={() => setOpen(undefined)} onChanged={reload} />}
      {editing && data && (
        <EditDialog board={data} editing={editing} onClose={() => setEditing(undefined)} onSaved={(item) => ((reveal.current = item.id), pick(item.instrument), reload())} />
      )}
      {deleting && <DeleteDialog item={deleting} onClose={() => setDeleting(undefined)} onDone={reload} />}
    </section>
  );
}

function NaipeCard({ item, onOpen, onEdit, onDelete }: { item: NaipeItem; onOpen: () => void; onEdit: () => void; onDelete: () => void }) {
  return (
    <article className="naipe-card">
      <button type="button" className="naipe-card__open" onClick={onOpen}>
        <span className="naipe-card__media">
          {item.isVideo ? (
            <video src={item.url} muted preload="metadata" playsInline aria-hidden="true" tabIndex={-1} />
          ) : (
            <img src={item.url} alt="" loading="lazy" />
          )}
          <span className="naipe-card__kind">
            <Icon name={item.isVideo ? 'video' : 'images'} />
            {item.isVideo ? 'Vídeo' : 'Imagem'}
          </span>
        </span>
        <span className="naipe-card__body">
          <strong className="naipe-card__title">{item.title}</strong>
          {item.description && <span className="naipe-card__text">{item.description}</span>}
          <span className="naipe-card__meta">
            {item.isVideo && (
              <span>
                <Icon name="play" /> {item.playCount}
              </span>
            )}
            <span>
              <Icon name="chat" /> {item.commentCount}
            </span>
            <span className="naipe-card__by">{item.createdBy}</span>
          </span>
        </span>
      </button>
      {item.canEdit && (
        <span className="naipe-card__tools">
          <button type="button" className="icon-btn" onClick={onEdit}>
            <Icon name="pencil" />
            <span className="sr-only">Editar {item.title}</span>
          </button>
          <button type="button" className="icon-btn icon-btn--danger" onClick={onDelete}>
            <Icon name="trash" />
            <span className="sr-only">Eliminar {item.title}</span>
          </button>
        </span>
      )}
    </article>
  );
}

function Pager({
  total,
  page,
  pages,
  pageSize,
  onPage,
  onPageSize,
}: {
  total: number;
  page: number;
  pages: number;
  pageSize: number;
  onPage: (page: number) => void;
  onPageSize: (size: number) => void;
}) {
  const id = useId();
  return (
    <nav className="naipe-pager" aria-label="Páginas">
      <span className="note">
        {total} {total === 1 ? 'conteúdo' : 'conteúdos'}
      </span>
      <span className="naipe-pager__nav">
        <button type="button" className="icon-btn" onClick={() => onPage(page - 1)} disabled={page <= 1}>
          <Icon name="chevronLeft" />
          <span className="sr-only">Página anterior</span>
        </button>
        <span>
          {page} / {pages}
        </span>
        <button type="button" className="icon-btn" onClick={() => onPage(page + 1)} disabled={page >= pages}>
          <Icon name="chevronRight" />
          <span className="sr-only">Página seguinte</span>
        </button>
      </span>
      <label htmlFor={id} className="naipe-pager__size">
        Por página
        <span className="control control--select control--sm">
          <select id={id} value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))}>
            {PAGE_SIZES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </span>
      </label>
    </nav>
  );
}

/** One item: the video or image, its description and numbers, and the comments. */
function ViewDialog({ item, onClose, onChanged }: { item: NaipeItem; onClose: () => void; onChanged: () => void }) {
  const [comments, setComments] = useState<NaipeComment[] | null>();
  const [draft, setDraft] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const played = useRef(false);
  const id = useId();

  useEffect(() => {
    naipesApi.comments(item.id).then((o) => setComments(o.kind === 'ok' ? o.data : null));
  }, [item.id]);

  const onPlay = () => {
    if (played.current) return; // one play per opening, as before
    played.current = true;
    naipesApi.played(item.id).then((o) => o.kind === 'ok' && onChanged());
  };

  const run = async (action: () => Promise<Outcome<NaipeComment[]>>, what: string) => {
    setBusy(true);
    const o = await action();
    setBusy(false);
    if (o.kind === 'ok') {
      setComments(o.data);
      setError(undefined);
      onChanged();
      return true;
    }
    setError(problem(o, what));
    return false;
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!draft.trim()) return;
    if (await run(() => naipesApi.addComment(item.id, draft.trim()), 'publicar o comentário')) setDraft('');
  };

  return (
    <Dialog title={item.title} size="lg" onClose={onClose}>
      <div className="naipe-view">
        <span className="member-badges">
          <span className="member-badge member-badge--position">{item.instrumentLabel}</span>
          <span className="member-badge">{item.isVideo ? 'Vídeo' : 'Imagem'}</span>
        </span>
        {item.isVideo ? (
          <video className="naipe-view__media" controls playsInline preload="metadata" onPlay={onPlay}>
            <source src={item.url} type={item.mimeType} />
            {item.mimeType === 'video/quicktime' && <source src={item.url} type="video/mp4" />}
            Este navegador não reproduz este formato de vídeo (gravações de iPhone em HEVC podem precisar do Safari).
          </video>
        ) : (
          <img className="naipe-view__media" src={item.url} alt={item.title} />
        )}
        {item.description && <p className="naipe-view__text">{item.description}</p>}
        <p className="note">
          {item.isVideo && `${item.playCount} ${item.playCount === 1 ? 'reprodução' : 'reproduções'} · `}Adicionado por {item.createdBy}
        </p>

        <section className="naipe-comments" aria-labelledby={`${id}-title`}>
          <h3 id={`${id}-title`} className="member-section__title">
            Comentários{comments ? ` (${comments.length})` : ''}
          </h3>
          <form className="form naipe-comments__form" onSubmit={submit}>
            <label htmlFor={`${id}-text`} className="sr-only">
              Comentário
            </label>
            <textarea
              id={`${id}-text`}
              rows={2}
              maxLength={MAX_COMMENT}
              placeholder="Escreve um comentário…"
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
            />
            <span className="naipe-comments__row">
              <small className="note">
                {draft.length} / {MAX_COMMENT}
              </small>
              <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !draft.trim()}>
                {busy && <span className="spinner spinner--small" aria-hidden="true" />}
                Publicar
              </button>
            </span>
          </form>
          {error && (
            <p className="form__error" role="alert">
              {error}
            </p>
          )}
          {comments === undefined ? (
            <Loading label="A carregar os comentários…" />
          ) : comments === null ? (
            <p className="note">Não foi possível carregar os comentários.</p>
          ) : comments.length === 0 ? (
            <p className="note">Ainda não há comentários.</p>
          ) : (
            <ul className="naipe-comments__list">
              {comments.map((c) => (
                <li key={c.id} className="naipe-comment">
                  <MemberFace avatarUrl={c.authorAvatarUrl || null} size={36} />
                  <span className="naipe-comment__body">
                    <span className="naipe-comment__who">
                      <strong>{c.authorName}</strong>
                      <small>{new Date(c.createdAt).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}</small>
                    </span>
                    <span className="naipe-comment__text">{c.text}</span>
                  </span>
                  {c.canDelete && (
                    <button
                      type="button"
                      className="icon-btn icon-btn--danger"
                      onClick={() => run(() => naipesApi.removeComment(item.id, c.id), 'eliminar o comentário')}
                      disabled={busy}
                    >
                      <Icon name="trash" />
                      <span className="sr-only">Eliminar comentário</span>
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </Dialog>
  );
}

/** "Adicionar vídeo" / "Adicionar imagem" (instrument, file, title, description, order) and "Editar". */
function EditDialog({ board, editing, onClose, onSaved }: { board: NaipeBoard; editing: Editing; onClose: () => void; onSaved: (item: NaipeItem) => void }) {
  const creating = editing.kind === 'new';
  const isVideo = editing.kind === 'new' ? editing.isVideo : editing.item.isVideo;
  const item = editing.kind === 'edit' ? editing.item : undefined;
  const [instrument, setInstrument] = useState(board.instrument ?? board.instrumentOptions[0]?.value ?? '');
  const [file, setFile] = useState<File>();
  const [title, setTitle] = useState(item?.title ?? '');
  const [description, setDescription] = useState(item?.description ?? '');
  const [sortOrder, setSortOrder] = useState(String(item?.sortOrder ?? board.nextSortOrder));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();
  const max = isVideo ? MAX_VIDEO : MAX_IMAGE;

  const pickFile = (picked?: File) => {
    if (!picked) return setFile(undefined);
    if (picked.size > max) {
      setFile(undefined);
      return setErrors({ ...errors, file: isVideo ? 'O vídeo não pode exceder 100 MB.' : 'A imagem não pode exceder 10 MB.' });
    }
    setErrors({ ...errors, file: '' });
    setFile(picked);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (creating && !file) return setErrors({ ...errors, file: 'O ficheiro é obrigatório.' });
    setBusy(true);
    setBanner(undefined);
    const o = creating
      ? await naipesApi.create({ file: file!, instrument, isVideo, title, description, sortOrder })
      : await naipesApi.update(item!.id, title, description, sortOrder.trim() ? Number(sortOrder.replace(',', '.')) : null);
    setBusy(false);
    if (o.kind === 'ok') {
      onSaved(o.data);
      onClose();
    } else if (o.kind === 'invalid') setErrors(o.errors);
    else {
      setErrors({});
      setBanner(problem(o, 'guardar'));
    }
  };

  const field = (key: string) => (errors[key] ? 'form__field form__field--error' : 'form__field');
  const error = (key: string) =>
    errors[key] && (
      <p className="form__error" role="alert">
        {errors[key]}
      </p>
    );

  return (
    <Dialog
      title={creating ? (isVideo ? 'Adicionar vídeo' : 'Adicionar imagem') : 'Editar conteúdo'}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="submit" form={`${id}-form`} className="btn btn--primary" disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            {busy && creating ? 'A enviar…' : 'Guardar'}
          </button>
        </>
      }
    >
      <form id={`${id}-form`} className="form" onSubmit={submit} noValidate>
        {banner && (
          <p className="form__banner" role="alert">
            {banner}
          </p>
        )}
        {creating && (
          <>
            <div className={field('instrument')}>
              <label htmlFor={`${id}-instrument`}>Instrumento</label>
              <span className="control control--select">
                <select id={`${id}-instrument`} value={instrument} onChange={(e) => setInstrument(e.target.value)}>
                  {board.instrumentOptions.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </select>
              </span>
              {error('instrument')}
            </div>
            <div className={field('file')}>
              <label htmlFor={`${id}-file`}>{isVideo ? 'Ficheiro de vídeo' : 'Ficheiro de imagem'}</label>
              <input id={`${id}-file`} type="file" accept={isVideo ? VIDEO_ACCEPT : IMAGE_ACCEPT} onChange={(e) => pickFile(e.target.files?.[0])} />
              <p className="form__hint">{isVideo ? 'MP4, MOV, WebM ou 3GP, até 100 MB.' : 'JPEG, PNG, WebP ou GIF, até 10 MB.'}</p>
              {error('file')}
            </div>
          </>
        )}
        <div className={field('title')}>
          <label htmlFor={`${id}-title`}>Título</label>
          <input
            id={`${id}-title`}
            type="text"
            maxLength={200}
            value={title}
            placeholder={creating ? 'Se ficar vazio, recebe um nome automático' : undefined}
            onChange={(e) => setTitle(e.target.value)}
          />
          {error('title')}
        </div>
        <div className={field('description')}>
          <label htmlFor={`${id}-description`}>Descrição</label>
          <textarea id={`${id}-description`} rows={3} maxLength={1000} value={description} onChange={(e) => setDescription(e.target.value)} />
          {error('description')}
        </div>
        <div className={field('sortOrder')}>
          <label htmlFor={`${id}-order`}>Ordem</label>
          <input id={`${id}-order`} type="number" inputMode="decimal" step="0.1" min="0.1" max="999.9" value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} />
          <p className="form__hint">Os números mais baixos aparecem primeiro; usa decimais (1.5) para pôr entre dois.</p>
          {error('sortOrder')}
        </div>
      </form>
    </Dialog>
  );
}

function DeleteDialog({ item, onClose, onDone }: { item: NaipeItem; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const confirm = async () => {
    setBusy(true);
    const o = await naipesApi.remove(item.id);
    setBusy(false);
    if (o.kind === 'ok') {
      onDone();
      onClose();
    } else setError(problem(o, 'eliminar'));
  };

  return (
    <Dialog
      title="Eliminar conteúdo"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={confirm} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            Eliminar
          </button>
        </>
      }
    >
      <p>
        Eliminar <strong>{item.title}</strong>? O ficheiro e os comentários vão com ele, sem volta atrás.
      </p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
