import { useEffect, useId, useRef, useState, type FormEvent, type KeyboardEvent, type ReactNode } from 'react';
import { Loading, useCurrentUser } from './App';
import { portal } from './content';
import {
  dateLabel,
  eventsApi,
  type EventAuthor,
  type EventDiscussion as Discussion,
  type EventPost,
  type EventSummary,
  type MemberOption,
  type Outcome,
} from './eventsApi';
import { Icon } from './icons';
import { loginTo } from './musicApi';

const BODY_MAX = 5000;
const COMMENT_MAX = 2000;

const problem = (o: Outcome<unknown>) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Não tens permissão para isto.'
      : o.kind === 'notfound'
        ? 'Isto já não existe. Recarrega a página.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : 'Não foi possível guardar. Tenta outra vez daqui a pouco.';

/** "agora", "há 5 min", "há 3 h", "ontem, 21:10", then "12 de outubro, 21:10" (and the year when it is not this one). */
export function when(iso: string, now = new Date()) {
  const d = new Date(iso);
  const minutes = Math.round((now.getTime() - d.getTime()) / 60000);
  const time = d.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });
  if (minutes < 1) return 'agora';
  if (minutes < 60) return `há ${minutes} min`;
  const yesterday = new Date(now);
  yesterday.setDate(now.getDate() - 1);
  if (d.toDateString() === now.toDateString()) return `há ${Math.round(minutes / 60)} h`;
  if (d.toDateString() === yesterday.toDateString()) return `ontem, ${time}`;
  const day = d.toLocaleDateString('pt-PT', { day: 'numeric', month: 'long', ...(d.getFullYear() !== now.getFullYear() ? { year: 'numeric' } : {}) });
  return `${day}, ${time}`;
}

/** @mentions stand out, as on the old page; the text itself is never HTML. */
function Text({ children }: { children: string }) {
  return (
    <p className="talk__text">
      {children.split(/(@[\p{L}\p{N}_]+)/u).map((part, i) =>
        i % 2 === 1 ? (
          <span key={i} className="talk__mention">
            {part}
          </span>
        ) : (
          part
        ),
      )}
    </p>
  );
}

/** A title taken from the start of the text (when none was given) is not repeated above it. */
const showsTitle = (p: EventPost) => !p.body.replace(/\s+/g, ' ').trim().startsWith(p.title.replace(/…$/, ''));

function Who({ author, at, edited, children }: { author: EventAuthor; at: string; edited: boolean; children?: ReactNode }) {
  return (
    <header className="talk__who">
      <img className="talk__avatar" src={author.avatarUrl} alt="" loading="lazy" width="44" height="44" />
      <div className="talk__meta">
        <p className="talk__name" title={author.fullName ?? undefined}>
          {author.name}
          {author.badge && <span className="talk__badge">{author.badge}</span>}
        </p>
        <p className="talk__time">
          <time dateTime={at} title={new Date(at).toLocaleString('pt-PT')}>
            {when(at)}
          </time>
          {edited && ' · editado'}
        </p>
      </div>
      {children}
    </header>
  );
}

/** Submit on Ctrl/⌘+Enter, as chat boxes do; Enter alone is a new line. */
const submitOnCtrlEnter = (e: KeyboardEvent<HTMLTextAreaElement>) => {
  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
    e.preventDefault();
    e.currentTarget.form?.requestSubmit();
  }
};

/**
 * /events/{id}/discussion - the event's conversation (React track 013; was a Blazor page). Signed-in members
 * post notes, comment, and offer or organise lifts ("boleias"); everything is one feed, pinned posts first
 * and then whatever moved last. Every rule is the server's (GET /api/events/{id}/discussion says what the
 * caller may do); each write answers the whole conversation, so the page never drifts.
 */
export default function EventDiscussion({ eventId }: { eventId: number }) {
  const [event, setEvent] = useState<EventSummary | 'missing' | null>();
  const [talk, setTalk] = useState<Discussion | 'signin' | null>();

  useEffect(() => {
    eventsApi.event(eventId).then((o) => setEvent(o.kind === 'ok' ? o.data.event : o.kind === 'notfound' ? 'missing' : null));
    eventsApi.discussion(eventId).then((o) => setTalk(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));
  }, [eventId]);

  useEffect(() => {
    document.title = event && event !== 'missing' ? `Discussão · ${event.name} · RTUB` : 'Discussão · RTUB';
  }, [event]);

  const back = (
    <a className="back-link" href={portal.event(eventId)}>
      <Icon name="arrow" />
      {event && event !== 'missing' ? event.name : 'Atuação'}
    </a>
  );

  if (event === 'missing') {
    return (
      <section className="page wrap talk-page">
        <a className="back-link" href={portal.events}>
          <Icon name="arrow" />
          Agenda
        </a>
        <div className="notice" role="status">
          <p>Esta atuação não existe ou já foi apagada.</p>
        </div>
      </section>
    );
  }

  return (
    <section className="page wrap talk-page" aria-labelledby="talk-title">
      {back}
      <header className="page__head talk-page__head">
        <p className="eyebrow">Conversa da atuação</p>
        <h1 id="talk-title" className="page__title">
          Discussão
        </h1>
        {event && (
          <p className="page__lead">
            {event.name} · {dateLabel(event)} · {event.location}
          </p>
        )}
      </header>

      {talk === undefined ? (
        <Loading label="A carregar a conversa…" />
      ) : talk === 'signin' ? (
        <div className="notice" role="status">
          <p>A discussão é da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(`/events/${eventId}/discussion`)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : talk === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir a conversa agora. Tenta outra vez daqui a pouco.</p>
        </div>
      ) : (
        <Feed eventId={eventId} talk={talk} onChange={setTalk} />
      )}
    </section>
  );
}

function Feed({ eventId, talk, onChange }: { eventId: number; talk: Discussion; onChange: (d: Discussion) => void }) {
  return (
    <div className="talk">
      <Composer eventId={eventId} onPosted={onChange} />
      {talk.posts.length === 0 ? (
        <div className="talk__empty">
          <Icon name="chat" />
          <p className="talk__empty-title">Ainda não há mensagens nesta atuação.</p>
          <p>Deixa uma nota, combina detalhes ou partilha uma dúvida.</p>
        </div>
      ) : (
        <ol className="talk__feed" aria-label="Mensagens">
          {talk.posts.map((p) => (
            <li key={p.id}>
              <Post eventId={eventId} post={p} canModerate={talk.canModerate} onChange={onChange} />
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

// ---------- writing a post or a lift offer ----------

function Composer({ eventId, onPosted }: { eventId: number; onPosted: (d: Discussion) => void }) {
  const { user } = useCurrentUser();
  const [mode, setMode] = useState<'note' | 'lift'>('note');
  const [title, setTitle] = useState('');
  const [withTitle, setWithTitle] = useState(false);
  const [body, setBody] = useState('');
  const [lift, setLift] = useState({ vehicle: '', seats: 4, notes: '' });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const ids = { body: useId(), title: useId(), vehicle: useId(), seats: useId(), notes: useId() };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const o =
      mode === 'note'
        ? await eventsApi.addPost(eventId, withTitle ? title : '', body)
        : await eventsApi.addTransport(eventId, lift.vehicle, lift.seats, lift.notes);
    setBusy(false);
    if (o.kind !== 'ok') return setErrors(o.kind === 'invalid' ? o.errors : { form: problem(o) });
    setErrors({});
    setTitle('');
    setWithTitle(false);
    setBody('');
    setLift({ vehicle: '', seats: 4, notes: '' });
    setMode('note');
    onPosted(o.data);
  };

  const error = (key: string) => errors[key] && <p className="form__error">{errors[key]}</p>;

  return (
    <form className="talk__composer" onSubmit={submit} noValidate>
      {user?.authenticated && <img className="talk__avatar" src={user.avatarUrl} alt="" width="44" height="44" />}
      <div className="talk__composer-main">
        <div className="talk__modes" role="group" aria-label="O que queres publicar">
          <button type="button" className={`chip-toggle${mode === 'note' ? ' is-on' : ''}`} aria-pressed={mode === 'note'} onClick={() => setMode('note')}>
            <Icon name="chat" />
            Mensagem
          </button>
          <button type="button" className={`chip-toggle${mode === 'lift' ? ' is-on' : ''}`} aria-pressed={mode === 'lift'} onClick={() => setMode('lift')}>
            <Icon name="car" />
            Oferecer boleia
          </button>
        </div>

        {mode === 'note' ? (
          <>
            {withTitle && (
              <div className={errors.title ? 'form__field form__field--error' : 'form__field'}>
                <label htmlFor={ids.title} className="sr-only">
                  Título
                </label>
                <input id={ids.title} className="talk__title-input" placeholder="Título" maxLength={120} value={title} onChange={(e) => setTitle(e.target.value)} />
                {error('title')}
              </div>
            )}
            <div className={errors.body ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor={ids.body} className="sr-only">
                Mensagem
              </label>
              <textarea
                id={ids.body}
                rows={body ? 4 : 2}
                placeholder="Escreve uma nota para a atuação…"
                maxLength={BODY_MAX}
                value={body}
                onChange={(e) => setBody(e.target.value)}
                onKeyDown={submitOnCtrlEnter}
              />
              {error('body')}
            </div>
            <div className="talk__composer-foot">
              {!withTitle && (
                <button type="button" className="link-btn" onClick={() => setWithTitle(true)}>
                  Adicionar título
                </button>
              )}
              <span className="talk__hint">Usa @alcunha para chamar alguém.</span>
              <button type="submit" className="btn btn--primary btn--sm" disabled={busy || body.trim().length === 0}>
                <Icon name="send" />
                {busy ? 'A publicar…' : 'Publicar'}
              </button>
            </div>
          </>
        ) : (
          <>
            <div className="talk__lift-fields">
              <div className={errors.vehicle ? 'form__field form__field--error' : 'form__field'}>
                <label htmlFor={ids.vehicle}>Viatura</label>
                <input id={ids.vehicle} placeholder="Ex.: Clio cinzento" maxLength={200} value={lift.vehicle} onChange={(e) => setLift({ ...lift, vehicle: e.target.value })} />
                {error('vehicle')}
              </div>
              <div className={errors.seats ? 'form__field form__field--error' : 'form__field'}>
                <label htmlFor={ids.seats}>Lugares</label>
                <input id={ids.seats} type="number" min={2} max={20} value={lift.seats} onChange={(e) => setLift({ ...lift, seats: Number(e.target.value) })} />
                {error('seats')}
              </div>
            </div>
            <div className={errors.notes ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor={ids.notes}>Notas</label>
              <textarea id={ids.notes} rows={2} maxLength={500} placeholder="Ex.: saída às 18h da residência" value={lift.notes} onChange={(e) => setLift({ ...lift, notes: e.target.value })} />
              {error('notes')}
            </div>
            <div className="talk__composer-foot">
              <span className="talk__hint">Depois juntas quem vai contigo.</span>
              <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !lift.vehicle.trim()}>
                <Icon name="car" />
                {busy ? 'A publicar…' : 'Oferecer boleia'}
              </button>
            </div>
          </>
        )}
        {error('form')}
      </div>
    </form>
  );
}

// ---------- one post ----------

function Post({ eventId, post, canModerate, onChange }: { eventId: number; post: EventPost; canModerate: boolean; onChange: (d: Discussion) => void }) {
  const [editing, setEditing] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const run = async (call: Promise<Outcome<Discussion>>, done?: () => void) => {
    setBusy(true);
    setError(undefined);
    const o = await call;
    setBusy(false);
    if (o.kind === 'ok') {
      done?.();
      onChange(o.data);
    } else setError(problem(o));
  };

  return (
    <article className={`talk__post${post.pinned ? ' talk__post--pinned' : ''}${post.transport ? ' talk__post--lift' : ''}`} aria-label={post.title}>
      <Who author={post.author} at={post.createdAt} edited={post.edited}>
        <div className="talk__flags">
          {post.pinned && (
            <span className="talk__flag" title="Fixado no topo">
              <Icon name="pin" />
              <span className="sr-only">Fixado</span>
            </span>
          )}
          {post.locked && (
            <span className="talk__flag" title="Comentários fechados">
              <Icon name="lock" />
              <span className="sr-only">Fechado</span>
            </span>
          )}
        </div>
      </Who>

      {editing && !post.transport ? (
        <PostEditor
          post={post}
          busy={busy}
          onCancel={() => setEditing(false)}
          onSave={(title, body) => run(eventsApi.editPost(eventId, post.id, title, body), () => setEditing(false))}
        />
      ) : post.transport ? (
        <Lift eventId={eventId} post={post} editing={editing} onDone={() => setEditing(false)} onChange={onChange} />
      ) : (
        <div className="talk__body">
          {showsTitle(post) && <h2 className="talk__title">{post.title}</h2>}
          <Text>{post.body}</Text>
        </div>
      )}

      {(post.canEdit || post.canDelete || canModerate) && !editing && (
        <div className="talk__actions">
          {post.canEdit && (
            <button type="button" className="link-btn" onClick={() => setEditing(true)}>
              <Icon name="pencil" />
              Editar
            </button>
          )}
          {canModerate && (
            <>
              <button type="button" className="link-btn" disabled={busy} onClick={() => run(eventsApi.setPostFlags(eventId, post.id, !post.pinned, post.locked))}>
                <Icon name="pin" />
                {post.pinned ? 'Desafixar' : 'Fixar'}
              </button>
              <button type="button" className="link-btn" disabled={busy} onClick={() => run(eventsApi.setPostFlags(eventId, post.id, post.pinned, !post.locked))}>
                <Icon name={post.locked ? 'unlock' : 'lock'} />
                {post.locked ? 'Reabrir comentários' : 'Fechar comentários'}
              </button>
            </>
          )}
          {post.canDelete &&
            (confirming ? (
              <span className="talk__confirm">
                Apagar esta publicação?
                <button type="button" className="btn btn--danger btn--sm" disabled={busy} onClick={() => run(eventsApi.deletePost(eventId, post.id))}>
                  Apagar
                </button>
                <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirming(false)}>
                  Cancelar
                </button>
              </span>
            ) : (
              <button type="button" className="link-btn link-btn--danger" onClick={() => setConfirming(true)}>
                <Icon name="trash" />
                Apagar
              </button>
            ))}
        </div>
      )}
      {error && <p className="form__error">{error}</p>}

      <Comments eventId={eventId} post={post} onChange={onChange} />
    </article>
  );
}

function PostEditor({ post, busy, onCancel, onSave }: { post: EventPost; busy: boolean; onCancel: () => void; onSave: (title: string, body: string) => void }) {
  const [title, setTitle] = useState(showsTitle(post) ? post.title : '');
  const [body, setBody] = useState(post.body);
  const ids = { title: useId(), body: useId() };
  return (
    <form
      className="talk__edit"
      onSubmit={(e) => {
        e.preventDefault();
        onSave(title, body);
      }}
    >
      <label htmlFor={ids.title} className="sr-only">
        Título
      </label>
      <input id={ids.title} className="talk__title-input" placeholder="Título (opcional)" maxLength={120} value={title} onChange={(e) => setTitle(e.target.value)} />
      <label htmlFor={ids.body} className="sr-only">
        Mensagem
      </label>
      <textarea id={ids.body} rows={4} maxLength={BODY_MAX} value={body} onChange={(e) => setBody(e.target.value)} onKeyDown={submitOnCtrlEnter} />
      <div className="talk__composer-foot">
        <button type="button" className="btn btn--ghost btn--sm" onClick={onCancel}>
          Cancelar
        </button>
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !body.trim()}>
          Guardar
        </button>
      </div>
    </form>
  );
}

// ---------- a lift offer ----------

function Lift({ eventId, post, editing, onDone, onChange }: { eventId: number; post: EventPost; editing: boolean; onDone: () => void; onChange: (d: Discussion) => void }) {
  const t = post.transport!;
  const [form, setForm] = useState({ vehicle: t.vehicle, seats: t.totalSeats, notes: t.notes ?? '' });
  const [query, setQuery] = useState('');
  const [members, setMembers] = useState<MemberOption[]>();
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const ids = { vehicle: useId(), seats: useId(), notes: useId(), search: useId() };
  const free = t.totalSeats - t.passengers.length;

  useEffect(() => {
    if (!t.canManage || !query.trim()) return setMembers(undefined);
    const timer = setTimeout(() => {
      eventsApi.passengerMembers(eventId, post.id, query.trim()).then((o) => setMembers(o.kind === 'ok' ? o.data : []));
    }, 250);
    return () => clearTimeout(timer);
  }, [eventId, post.id, query, t.canManage, t.passengers.length]);

  const run = async (call: Promise<Outcome<Discussion>>, done?: () => void) => {
    setBusy(true);
    const o = await call;
    setBusy(false);
    if (o.kind === 'ok') {
      setErrors({});
      done?.();
      onChange(o.data);
    } else setErrors(o.kind === 'invalid' ? o.errors : { form: problem(o) });
  };

  if (editing) {
    return (
      <form
        className="talk__edit"
        onSubmit={(e) => {
          e.preventDefault();
          run(eventsApi.editTransport(eventId, post.id, form.vehicle, form.seats, form.notes), onDone);
        }}
      >
        <div className="talk__lift-fields">
          <div className="form__field">
            <label htmlFor={ids.vehicle}>Viatura</label>
            <input id={ids.vehicle} maxLength={200} value={form.vehicle} onChange={(e) => setForm({ ...form, vehicle: e.target.value })} />
            {errors.vehicle && <p className="form__error">{errors.vehicle}</p>}
          </div>
          <div className="form__field">
            <label htmlFor={ids.seats}>Lugares</label>
            <input id={ids.seats} type="number" min={2} max={20} value={form.seats} onChange={(e) => setForm({ ...form, seats: Number(e.target.value) })} />
            {errors.seats && <p className="form__error">{errors.seats}</p>}
          </div>
        </div>
        <div className="form__field">
          <label htmlFor={ids.notes}>Notas</label>
          <textarea id={ids.notes} rows={2} maxLength={500} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
          {errors.notes && <p className="form__error">{errors.notes}</p>}
        </div>
        {errors.form && <p className="form__error">{errors.form}</p>}
        <div className="talk__composer-foot">
          <button type="button" className="btn btn--ghost btn--sm" onClick={onDone}>
            Cancelar
          </button>
          <button type="submit" className="btn btn--primary btn--sm" disabled={busy}>
            Guardar
          </button>
        </div>
      </form>
    );
  }

  return (
    <div className="talk__lift">
      <div className="talk__lift-head">
        <Icon name="car" />
        <div>
          <p className="talk__lift-title">Boleia · {t.vehicle}</p>
          {t.notes && <p className="talk__lift-notes">{t.notes}</p>}
        </div>
        <span className={`talk__seats${free === 0 ? ' is-full' : ''}`}>
          {t.passengers.length}/{t.totalSeats} lugares
        </span>
      </div>
      {t.passengers.length === 0 ? (
        <p className="note">Ainda ninguém nesta boleia.</p>
      ) : (
        <ul className="talk__passengers" aria-label="Quem vai nesta boleia">
          {t.passengers.map((p) => (
            <li key={p.id} className="talk__passenger" title={p.member.fullName ?? undefined}>
              <img src={p.member.avatarUrl} alt="" width="28" height="28" loading="lazy" />
              {p.member.name}
              {p.canRemove && (
                <button
                  type="button"
                  className="icon-btn icon-btn--sm"
                  disabled={busy}
                  onClick={() => run(eventsApi.removePassenger(eventId, post.id, p.id))}
                  aria-label={p.mine ? 'Sair desta boleia' : `Tirar ${p.member.name} da boleia`}
                >
                  <Icon name="close" />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
      {t.canManage && free > 0 && (
        <div className="talk__seat-picker">
          <label className="control control--sm" htmlFor={ids.search}>
            <span className="sr-only">Juntar um membro à boleia</span>
            <Icon name="search" />
            <input id={ids.search} type="search" placeholder="Juntar alguém (alcunha ou nome)" value={query} onChange={(e) => setQuery(e.target.value)} />
          </label>
          {members && members.length === 0 && <p className="note">Ninguém encontrado.</p>}
          {members && members.length > 0 && (
            <ul className="talk__picks">
              {members.map((m) => (
                <li key={m.id}>
                  <button type="button" disabled={busy} onClick={() => run(eventsApi.addPassenger(eventId, post.id, m.id), () => setQuery(''))}>
                    <img src={m.avatarUrl} alt="" width="28" height="28" loading="lazy" />
                    <span>
                      {m.name}
                      {m.fullName && m.fullName !== m.name && <small>{m.fullName}</small>}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
      {(errors.userId || errors.form) && <p className="form__error">{errors.userId ?? errors.form}</p>}
    </div>
  );
}

// ---------- comments ----------

function Comments({ eventId, post, onChange }: { eventId: number; post: EventPost; onChange: (d: Discussion) => void }) {
  const [body, setBody] = useState('');
  const [editing, setEditing] = useState<{ id: number; body: string }>();
  const [confirming, setConfirming] = useState<number>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const inputId = useId();
  const box = useRef<HTMLTextAreaElement>(null);

  const run = async (call: Promise<Outcome<Discussion>>, done?: () => void) => {
    setBusy(true);
    setError(undefined);
    const o = await call;
    setBusy(false);
    setConfirming(undefined);
    if (o.kind === 'ok') {
      done?.();
      onChange(o.data);
    } else setError(problem(o));
  };

  return (
    <div className="talk__comments">
      {post.comments.length > 0 && (
        <ol className="talk__thread" aria-label={`Comentários (${post.comments.length})`}>
          {post.comments.map((c) => (
            <li key={c.id} className="talk__comment">
              <Who author={c.author} at={c.createdAt} edited={c.edited} />
              {editing?.id === c.id ? (
                <form
                  className="talk__reply"
                  onSubmit={(e) => {
                    e.preventDefault();
                    run(eventsApi.editComment(eventId, c.id, editing.body), () => setEditing(undefined));
                  }}
                >
                  <label htmlFor={`${inputId}-edit`} className="sr-only">
                    Editar comentário
                  </label>
                  <textarea id={`${inputId}-edit`} rows={2} maxLength={COMMENT_MAX} value={editing.body} onChange={(e) => setEditing({ id: c.id, body: e.target.value })} onKeyDown={submitOnCtrlEnter} />
                  <button type="button" className="btn btn--ghost btn--sm" onClick={() => setEditing(undefined)}>
                    Cancelar
                  </button>
                  <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !editing.body.trim()}>
                    Guardar
                  </button>
                </form>
              ) : (
                <Text>{c.body}</Text>
              )}
              {(c.canEdit || c.canDelete) && editing?.id !== c.id && (
                <div className="talk__actions talk__actions--small">
                  {c.canEdit && (
                    <button type="button" className="link-btn" onClick={() => setEditing({ id: c.id, body: c.body })}>
                      Editar
                    </button>
                  )}
                  {c.canDelete &&
                    (confirming === c.id ? (
                      <span className="talk__confirm">
                        Apagar?
                        <button type="button" className="btn btn--danger btn--sm" disabled={busy} onClick={() => run(eventsApi.deleteComment(eventId, c.id))}>
                          Apagar
                        </button>
                        <button type="button" className="btn btn--ghost btn--sm" onClick={() => setConfirming(undefined)}>
                          Cancelar
                        </button>
                      </span>
                    ) : (
                      <button type="button" className="link-btn link-btn--danger" onClick={() => setConfirming(c.id)}>
                        Apagar
                      </button>
                    ))}
                </div>
              )}
            </li>
          ))}
        </ol>
      )}
      {post.canComment ? (
        <form
          className="talk__reply"
          onSubmit={(e) => {
            e.preventDefault();
            run(eventsApi.addComment(eventId, post.id, body), () => setBody(''));
          }}
        >
          <label htmlFor={inputId} className="sr-only">
            Comentar
          </label>
          <textarea
            id={inputId}
            ref={box}
            rows={1}
            placeholder="Comentar…"
            maxLength={COMMENT_MAX}
            value={body}
            onChange={(e) => setBody(e.target.value)}
            onKeyDown={submitOnCtrlEnter}
          />
          <button type="submit" className="icon-btn" disabled={busy || !body.trim()}>
            <Icon name="send" />
            <span className="sr-only">Enviar comentário</span>
          </button>
        </form>
      ) : (
        <p className="talk__closed">
          <Icon name="lock" />
          Comentários fechados.
        </p>
      )}
      {error && <p className="form__error">{error}</p>}
    </div>
  );
}
