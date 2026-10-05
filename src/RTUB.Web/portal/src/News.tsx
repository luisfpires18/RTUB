import { useEffect, useId, useRef, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { newsDate, type NewsFeed, type NewsPost } from './api';
import { Dialog } from './Dialog';
import { call, type Outcome } from './eventsApi';
import { Icon } from './icons';

// /api/news (Endpoints/NewsEndpoints.cs, React track 025). Everyone, visitors included, reads published posts; Admin and
// Owner also see drafts and write. The server decides every rule (NewsService); hiding a button here is only a convenience.

type Input = { title: string; body: string; publish: boolean };

const api = {
  feed: (page: number) => call<NewsFeed>('GET', `/api/news?page=${page}`),
  create: (input: Input) => call<NewsPost>('POST', '/api/news', input),
  update: (id: number, input: Input) => call<NewsPost>('PUT', `/api/news/${id}`, input),
  publish: (id: number) => call<NewsPost>('POST', `/api/news/${id}/publish`),
  unpublish: (id: number) => call<NewsPost>('POST', `/api/news/${id}/unpublish`),
  remove: (id: number) => call<void>('DELETE', `/api/news/${id}`),
};

const TITLE_MAX = 150;
const BODY_MAX = 5000;

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Só Admin ou Owner gerem as Novidades.'
      : o.kind === 'notfound'
        ? 'Esta publicação já não existe.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

/**
 * /news - Novidades (React track 025): the RTUB's public wall of text posts, newest first, signed "RTUB". Admin and
 * Owner write posts (as a draft or published straight away), edit, publish / unpublish and delete them; their drafts
 * sit above the feed, seen by no one else. A shared /news#post-12 link scrolls to that post.
 */
export default function News() {
  const [feed, setFeed] = useState<NewsFeed | null>();
  const [page, setPage] = useState(1);
  const [more, setMore] = useState<'idle' | 'busy' | 'failed'>('idle');
  const [editing, setEditing] = useState<NewsPost | 'new'>();
  const [deleting, setDeleting] = useState<NewsPost>();
  const scrolled = useRef(false);
  const draftsTitle = useId();

  useEffect(() => {
    document.title = 'Novidades · RTUB';
  }, []);

  const load = () =>
    api.feed(1).then((o) => {
      setPage(1);
      setMore('idle');
      setFeed(o.kind === 'ok' ? o.data : null);
    });

  useEffect(() => {
    load();
  }, []);

  useEffect(() => {
    if (!feed || scrolled.current) return;
    scrolled.current = true;
    if (location.hash) document.getElementById(location.hash.slice(1))?.scrollIntoView();
  }, [feed]);

  const loadMore = async () => {
    if (!feed) return;
    setMore('busy');
    const o = await api.feed(page + 1);
    if (o.kind !== 'ok') return setMore('failed');
    setMore('idle');
    setPage(page + 1);
    const known = new Set(feed.posts.map((p) => p.id));
    setFeed({ ...feed, posts: [...feed.posts, ...o.data.posts.filter((p) => !known.has(p.id))], hasMore: o.data.hasMore });
  };

  const post = (p: NewsPost) => (
    <li key={p.id}>
      <Post post={p} manage={feed?.canManage === true} onEdit={() => setEditing(p)} onDelete={() => setDeleting(p)} onChange={load} />
    </li>
  );

  return (
    <section className="page wrap talk-page news-page" aria-labelledby="news-page-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Novidades</p>
          <h1 id="news-page-title" className="page__title">
            Novidades
          </h1>
          <p className="page__lead">Anúncios e crónicas do que a tuna anda a fazer, contados pela própria RTUB.</p>
        </div>
        {feed?.canManage && (
          <div className="events-page__actions">
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setEditing('new')}>
              <Icon name="plus" />
              Nova publicação
            </button>
          </div>
        )}
      </header>

      {feed === undefined ? (
        <Loading label="A carregar as novidades…" />
      ) : feed === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir as novidades agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={load}>
            Tentar novamente
          </button>
        </div>
      ) : (
        <div className="talk">
          {feed.drafts.length > 0 && (
            <section className="news-drafts" aria-labelledby={draftsTitle}>
              <h2 id={draftsTitle} className="news-drafts__title">
                Rascunhos <span className="note">· só Admin e Owner os veem</span>
              </h2>
              <ol className="talk__feed">{feed.drafts.map(post)}</ol>
            </section>
          )}

          {feed.posts.length === 0 ? (
            <div className="talk__empty">
              <Icon name="chat" />
              <p className="talk__empty-title">Ainda não há novidades</p>
              <p>Quando a RTUB publicar alguma coisa, aparece aqui.</p>
            </div>
          ) : (
            <ol className="talk__feed" aria-label="Publicações">
              {feed.posts.map(post)}
            </ol>
          )}

          {feed.hasMore && (
            <button type="button" className="btn btn--ghost news-more" onClick={loadMore} disabled={more === 'busy'}>
              {more === 'busy' && <span className="spinner spinner--small" aria-hidden="true" />}
              Mostrar mais
            </button>
          )}
          {more === 'failed' && (
            <p className="form__banner" role="alert">
              Não foi possível carregar mais publicações. Tenta outra vez.
            </p>
          )}
        </div>
      )}

      {editing && <FormDialog post={editing === 'new' ? undefined : editing} onClose={() => setEditing(undefined)} onSaved={load} />}
      {deleting && <DeleteDialog post={deleting} onClose={() => setDeleting(undefined)} onDone={load} />}
    </section>
  );
}

function Post({
  post,
  manage,
  onEdit,
  onDelete,
  onChange,
}: {
  post: NewsPost;
  manage: boolean;
  onEdit: () => void;
  onDelete: () => void;
  onChange: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const draft = post.publishedAt === null;

  const toggle = async () => {
    setBusy(true);
    setError(undefined);
    const o = draft ? await api.publish(post.id) : await api.unpublish(post.id);
    setBusy(false);
    if (o.kind === 'ok') onChange();
    else setError(problem(o, draft ? 'publicar' : 'despublicar'));
  };

  return (
    <article id={`post-${post.id}`} className={draft ? 'talk__post news-post news-post--draft' : 'talk__post news-post'}>
      <header className="talk__who">
        <img className="talk__avatar news-post__avatar" src="/icons/rtub-logo-192.png" alt="" width="44" height="44" />
        <div className="talk__meta">
          <p className="talk__name">
            RTUB
            {draft && <span className="talk__badge">Rascunho</span>}
          </p>
          <p className="talk__time">
            {post.publishedAt ? <time dateTime={post.publishedAt}>{newsDate(post.publishedAt)}</time> : 'Ainda não publicado'}
            {manage && ` · escrito por ${post.authorName ?? 'uma conta eliminada'}`}
          </p>
        </div>
      </header>

      <div className="talk__body">
        {post.title && <h2 className="talk__title">{post.title}</h2>}
        <p className="talk__text">{post.body}</p>
      </div>

      {manage && (
        <div className="talk__actions">
          <button type="button" className="link-btn" onClick={onEdit} disabled={busy}>
            <Icon name="pencil" />
            Editar
          </button>
          <button type="button" className="link-btn" onClick={toggle} disabled={busy}>
            <Icon name={draft ? 'send' : 'restore'} />
            {draft ? 'Publicar' : 'Despublicar'}
          </button>
          <button type="button" className="link-btn link-btn--danger" onClick={onDelete} disabled={busy}>
            <Icon name="trash" />
            Eliminar
          </button>
        </div>
      )}
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </article>
  );
}

/** "Nova publicação" (save as a draft or publish now) / "Editar publicação" (title and text; the state stays). */
function FormDialog({ post, onClose, onSaved }: { post?: NewsPost; onClose: () => void; onSaved: () => void }) {
  const [title, setTitle] = useState(post?.title ?? '');
  const [body, setBody] = useState(post?.body ?? '');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const save = async (publish: boolean) => {
    if (!body.trim()) return setErrors({ body: 'Escreve o texto da publicação.' });
    setBusy(true);
    setBanner(undefined);
    const input = { title, body, publish };
    const o = post ? await api.update(post.id, input) : await api.create(input);
    setBusy(false);
    if (o.kind === 'ok') {
      onSaved();
      onClose();
    } else if (o.kind === 'invalid') setErrors(o.errors);
    else setBanner(problem(o, 'guardar'));
  };

  const submit = (e: FormEvent) => {
    e.preventDefault();
    save(!post);
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
      title={post ? 'Editar publicação' : 'Nova publicação'}
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          {!post && (
            <button type="button" className="btn btn--ghost" onClick={() => save(false)} disabled={busy}>
              Guardar rascunho
            </button>
          )}
          <button type="submit" form={`${fid}-form`} className="btn btn--primary" disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            {post ? 'Guardar' : 'Publicar'}
          </button>
        </>
      }
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        {banner && (
          <p className="form__banner" role="alert">
            {banner}
          </p>
        )}
        <div className={field('title')}>
          <label htmlFor={`${fid}-title`}>Título (opcional)</label>
          <input id={`${fid}-title`} type="text" maxLength={TITLE_MAX} value={title} onChange={(e) => setTitle(e.target.value)} />
          {error('title')}
        </div>
        <div className={field('body')}>
          <label htmlFor={`${fid}-body`}>Texto</label>
          <textarea
            id={`${fid}-body`}
            rows={8}
            maxLength={BODY_MAX}
            value={body}
            aria-describedby={`${fid}-count`}
            onChange={(e) => {
              setBody(e.target.value);
              if (errors.body) setErrors({ ...errors, body: '' });
            }}
          />
          <p id={`${fid}-count`} className="form__hint">
            {body.length} / {BODY_MAX} caracteres
          </p>
          {error('body')}
        </div>
        {!post && <p className="note">Um rascunho só é visto por Admin e Owner até ser publicado.</p>}
      </form>
    </Dialog>
  );
}

function DeleteDialog({ post, onClose, onDone }: { post: NewsPost; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const remove = async () => {
    setBusy(true);
    const o = await api.remove(post.id);
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') {
      onDone();
      onClose();
    } else setError(problem(o, 'eliminar'));
  };

  return (
    <Dialog
      title="Eliminar publicação"
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
        Eliminar <strong>{post.title ?? 'esta publicação'}</strong>
        {post.publishedAt ? ' das Novidades' : ''}?
      </p>
      <p className="warning">
        <Icon name="warning" />
        Não dá para desfazer.
      </p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
