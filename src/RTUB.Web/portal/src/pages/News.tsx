import { useEffect, useId, useRef, useState } from 'react';
import { Loading } from '../components/AppShared';
import { newsDate, type NewsFeed, type NewsPost } from '../data/api';
import { Icon } from '../components/icons';
import { MemberBadge } from '../components/MemberBadge';
import { RetryNotice } from '../components/Notice';
import { api, problem, type Tab } from '../data/newsApi';
import { FormDialog, DeleteDialog } from '../components/NewsDialogs';

// /api/news (Endpoints/NewsEndpoints.cs, React track 025). Everyone, visitors included, reads published posts; Admin and
// Owner also see drafts and write. The server decides every rule (NewsService); hiding a button here is only a convenience.

const TABS: [Tab, string][] = [
  ['all', 'Todas'],
  ['public', 'Públicas'],
  ['private', 'Privadas'],
];

/**
 * /news - Novidades (React track 025): the RTUB's public wall of text posts, newest first, signed "RTUB". Admin and
 * Owner write posts (as a draft or published straight away), edit, publish / unpublish and delete them; a Mod does the same
 * except delete. Drafts sit above the feed, seen by Mod and above. A shared /news#post-12 link scrolls to that post.
 */
export default function News() {
  const [feed, setFeed] = useState<NewsFeed | null>();
  const [page, setPage] = useState(1);
  const [more, setMore] = useState<'idle' | 'busy' | 'failed'>('idle');
  const [tab, setTab] = useState<Tab>('all');
  const [editing, setEditing] = useState<NewsPost | 'new'>();
  const [deleting, setDeleting] = useState<NewsPost>();
  const scrolled = useRef(false);
  const draftsTitle = useId();

  useEffect(() => {
    document.title = 'Novidades · RTUB';
  }, []);

  const load = (t: Tab = tab) =>
    api.feed(1, t).then((o) => {
      setPage(1);
      setMore('idle');
      setFeed(o.kind === 'ok' ? o.data : null);
    });

  useEffect(() => {
    load();
  }, []);

  // Task 077: /news?new=1 (from /pending-actions → Novidades → Criar novidade) opens the composer for whoever may write.
  const askedNew = useRef(false);
  useEffect(() => {
    if (!feed || askedNew.current) return;
    askedNew.current = true;
    if (new URLSearchParams(location.search).get('new') !== '1') return;
    history.replaceState(null, '', location.pathname + location.hash);
    if (feed.canWrite) setEditing('new');
  }, [feed]);

  const choose = (t: Tab) => {
    setTab(t);
    load(t);
  };

  useEffect(() => {
    if (!feed || scrolled.current) return;
    scrolled.current = true;
    if (location.hash) document.getElementById(location.hash.slice(1))?.scrollIntoView();
  }, [feed]);

  const loadMore = async () => {
    if (!feed) return;
    setMore('busy');
    const o = await api.feed(page + 1, tab);
    if (o.kind !== 'ok') return setMore('failed');
    setMore('idle');
    setPage(page + 1);
    const known = new Set(feed.posts.map((p) => p.id));
    setFeed({ ...feed, posts: [...feed.posts, ...o.data.posts.filter((p) => !known.has(p.id))], hasMore: o.data.hasMore });
  };

  const post = (p: NewsPost) => (
    <li key={p.id}>
      <Post
        post={p}
        canWrite={feed?.canWrite === true}
        canManage={feed?.canManage === true}
        onEdit={() => setEditing(p)}
        onDelete={() => setDeleting(p)}
        onChange={() => load()}
      />
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
        {feed?.canWrite && (
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
        <RetryNotice onRetry={() => load()}>Não conseguimos abrir as novidades agora.</RetryNotice>
      ) : (
        <div className="talk">
          {feed.signedIn && (
            <div className="news-tabs" role="group" aria-label="Filtrar publicações">
              {TABS.map(([id, label]) => (
                <button key={id} type="button" aria-pressed={tab === id} onClick={() => choose(id)}>
                  {label}
                </button>
              ))}
            </div>
          )}

          {feed.drafts.length > 0 && (
            <section className="news-drafts" aria-labelledby={draftsTitle}>
              <h2 id={draftsTitle} className="news-drafts__title">
                Rascunhos <span className="note">· ainda não publicados</span>
              </h2>
              <ol className="talk__feed">{feed.drafts.map(post)}</ol>
            </section>
          )}

          {feed.posts.length === 0 ? (
            <div className="talk__empty">
              <Icon name="chat" />
              <p className="talk__empty-title">Ainda não há novidades</p>
              <p>{tab === 'all' ? 'Quando a RTUB publicar alguma coisa, aparece aqui.' : 'Não há publicações nesta categoria.'}</p>
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

      {editing && <FormDialog post={editing === 'new' ? undefined : editing} onClose={() => setEditing(undefined)} onSaved={() => load()} />}
      {deleting && <DeleteDialog post={deleting} onClose={() => setDeleting(undefined)} onDone={() => load()} />}
    </section>
  );
}

function Post({
  post,
  canWrite,
  canManage,
  onEdit,
  onDelete,
  onChange,
}: {
  post: NewsPost;
  canWrite: boolean;
  canManage: boolean;
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
        <img className="talk__avatar news-post__avatar" src="/icons/rtub-logo-352.webp" alt="" width="44" height="44" />
        <div className="talk__meta">
          <p className="talk__name">
            RTUB
            {draft && <MemberBadge>Rascunho</MemberBadge>}
            {post.membersOnly && <span className="tag tag--private">Só membros</span>}
          </p>
          <p className="talk__time">
            {post.publishedAt ? <time dateTime={post.publishedAt}>{newsDate(post.publishedAt)}</time> : 'Ainda não publicado'}
            {canWrite && ` · escrito por ${post.authorName ?? 'uma conta eliminada'}`}
          </p>
        </div>
      </header>

      <div className="talk__body">
        {post.title && <h2 className="talk__title">{post.title}</h2>}
        <p className="talk__text">{post.body}</p>
      </div>

      {canWrite && (
        <div className="talk__actions">
          <button type="button" className="link-btn" onClick={onEdit} disabled={busy}>
            <Icon name="pencil" />
            Editar
          </button>
          <button type="button" className="link-btn" onClick={toggle} disabled={busy}>
            <Icon name={draft ? 'send' : 'restore'} />
            {draft ? 'Publicar' : 'Despublicar'}
          </button>
          {canManage && (
            <button type="button" className="link-btn link-btn--danger" onClick={onDelete} disabled={busy}>
              <Icon name="trash" />
              Eliminar
            </button>
          )}
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
