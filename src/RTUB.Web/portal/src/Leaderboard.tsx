import { useEffect, useId, useState, type FormEvent } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { Dialog } from './Dialog';
import { call, type Outcome } from './eventsApi';
import { Icon } from './icons';
import { MemberFace } from './MemberDialogs';
import { loginTo } from './musicApi';

// /api/leaderboard (Endpoints/LeaderboardEndpoints.cs, React track 019). Signed-in members only; the server decides
// every rule (LeaderboardService). Hiding a button here is only a convenience.

type Option = { value: string; label: string };
type Entry = {
  position: number;
  id: string;
  displayName: string;
  fullName: string | null;
  avatarUrl: string | null;
  level: number;
  rankName: string;
  xp: number;
  rehearsals: number;
  events: number;
};
type Level = { level: number; name: string; xpThreshold: number };
type Story = { title: string; content: string; isActive: boolean };
type Table = { fiscalYears: Option[]; fiscalYear: string; total: number; entries: Entry[]; levels: Level[]; story: Story | null };
type Progress = { level: number; rankName: string; xp: number; xpToNextLevel: number; progressPercentage: number; isMaxLevel: boolean; nextRankName: string | null };
type Breakdown = {
  totalXp: number;
  rehearsalCount: number;
  rehearsalXpPerUnit: number;
  rehearsalXpTotal: number;
  eventsByType: { type: string; count: number; xpPerUnit: number; totalXp: number }[];
};
type Activity = { date: string; name: string; type: string; xpEarned: number; isRehearsal: boolean };
type MemberDetail = { id: string; displayName: string; fullName: string | null; avatarUrl: string | null; progress: Progress; breakdown: Breakdown; activities: Activity[] };
type Comment = {
  id: number;
  authorName: string;
  authorAvatarUrl: string | null;
  text: string;
  createdAt: string;
  likes: number;
  likedByMe: boolean;
  canDelete: boolean;
  likedBy: string[];
};

const query = (params: Record<string, string>) => {
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v) p.set(k, v);
  const s = p.toString();
  return s ? `?${s}` : '';
};
const member = (id: string) => `/api/leaderboard/members/${encodeURIComponent(id)}`;

const api = {
  table: (fiscalYear: string, q: string) => call<Table>('GET', `/api/leaderboard${query({ fiscalYear, q })}`),
  member: (id: string, fiscalYear: string) => call<MemberDetail>('GET', `${member(id)}${query({ fiscalYear })}`),
  comments: (id: string) => call<Comment[]>('GET', `${member(id)}/comments`),
  comment: (id: string, text: string) => call<Comment[]>('POST', `${member(id)}/comments`, { text }),
  like: (commentId: number) => call<{ liked: boolean }>('POST', `/api/leaderboard/comments/${commentId}/like`),
  remove: (commentId: number) => call<void>('DELETE', `/api/leaderboard/comments/${commentId}`),
};

const MAX_COMMENT = 1000;
const PAGE = 20;
const medal = (position: number) => ['🥇', '🥈', '🥉'][position - 1] ?? String(position);

const problem = (o: Outcome<unknown>, what: string) =>
  o.kind === 'invalid'
    ? Object.values(o.errors)[0]
    : o.kind === 'forbidden'
      ? 'Não tem permissão para esta ação.'
      : o.kind === 'notfound'
        ? 'Já não existe.'
        : o.kind === 'signin'
          ? 'A sessão terminou. Entra outra vez.'
          : `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;

/** As the old comments: "agora", minutes, hours, days, then the date. The server's times are UTC. */
export function ago(iso: string, now = Date.now()) {
  const date = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : `${iso}Z`);
  const minutes = (now - date.getTime()) / 60000;
  if (minutes < 1) return 'agora';
  if (minutes < 60) return `${Math.floor(minutes)}m`;
  if (minutes < 60 * 24) return `${Math.floor(minutes / 60)}h`;
  if (minutes < 60 * 24 * 7) return `${Math.floor(minutes / 1440)}d`;
  return date.toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

/**
 * /leaderboard - Tabela de Classificação (React track 019; was the Blazor page). Signed-in members only. Everyone's XP
 * from rehearsals and events, ranked by level then XP, for one fiscal year or every year; a row opens the member's
 * details and comments. Admin and Owner edit the explanation shown above the levels.
 */
export default function Leaderboard() {
  const [table, setTable] = useState<Table | 'signin' | null>();
  const [fiscalYear, setFiscalYear] = useState(() => new URLSearchParams(location.search).get('fiscalYear') ?? '');
  const [text, setText] = useState('');
  const [q, setQ] = useState('');
  const [shown, setShown] = useState(PAGE);
  const [open, setOpen] = useState<string | null>(null);
  const ids = { q: useId(), year: useId() };

  useEffect(() => {
    document.title = 'Classificação · RTUB';
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  const load = () =>
    api.table(fiscalYear, q).then((o) => setTable(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));

  useEffect(() => {
    const url = new URL(location.href);
    if (fiscalYear) url.searchParams.set('fiscalYear', fiscalYear);
    else url.searchParams.delete('fiscalYear');
    history.replaceState(null, '', url.pathname + url.search);
    setShown(PAGE);
    load();
  }, [fiscalYear, q]);

  const data = table && table !== 'signin' ? table : null;

  return (
    <section className="page wrap events-page leaderboard-page" aria-labelledby="leaderboard-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="leaderboard-title" className="page__title">
            Tabela de Classificação
          </h1>
          <p className="page__lead">Cada ensaio e cada atuação contam XP; o XP sobe o nível.</p>
        </div>
        <div className="events-page__actions">
          <a className="btn btn--ghost btn--sm" href={portal.hallOfFame}>
            <Icon name="star" />
            Hall of Fame
          </a>
        </div>
      </header>

      {table === 'signin' ? (
        <div className="notice" role="status">
          <p>A classificação é da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.leaderboard)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : table === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir a classificação agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={load}>
            Tentar novamente
          </button>
        </div>
      ) : !data ? (
        <Loading label="A carregar a classificação…" />
      ) : (
        <>
          <details className="who__more leaderboard-levels">
            <summary>Níveis de Alcoolismo</summary>
            {data.story?.isActive && (
              <div className="leaderboard-story">
                <p className="leaderboard-story__title">
                  <strong>{data.story.title}</strong>
                </p>
                <p className="leaderboard-story__text">{data.story.content}</p>
              </div>
            )}
            <ol className="leaderboard-level-list">
              {data.levels.map((l) => (
                <li key={l.level} className={l.level === data.levels.length ? 'is-last' : undefined}>
                  <span className="leaderboard-level-list__n">Nível {l.level}</span>
                  <strong>{l.name}</strong>
                  <span className="note">{l.xpThreshold} XP</span>
                </li>
              ))}
            </ol>
          </details>

          <div className="events-tools" role="search">
            <label className="control" htmlFor={ids.q}>
              <span className="sr-only">Procurar na classificação</span>
              <Icon name="search" />
              <input id={ids.q} type="search" placeholder="Procurar por nome, alcunha ou nível" value={text} onChange={(e) => setText(e.target.value)} />
            </label>
            <label className="control control--select" htmlFor={ids.year}>
              <span className="sr-only">Ano letivo</span>
              <select id={ids.year} value={fiscalYear} onChange={(e) => setFiscalYear(e.target.value)}>
                <option value="">Todos os anos</option>
                {data.fiscalYears.map((y) => (
                  <option key={y.value} value={y.value}>
                    {y.label}
                  </option>
                ))}
              </select>
            </label>
          </div>

          {data.entries.length === 0 ? (
            <p className="note">{data.total === 0 ? 'Ainda não há membros classificados.' : 'Ninguém na classificação corresponde à pesquisa.'}</p>
          ) : (
            <>
              <ol className="leaderboard-rows" aria-label="Classificação">
                {data.entries.slice(0, shown).map((e) => (
                  <li key={e.id}>
                    <button type="button" className={`leaderboard-row${e.position <= 3 ? ` leaderboard-row--top${e.position}` : ''}`} onClick={() => setOpen(e.id)}>
                      <span className="leaderboard-row__pos" aria-label={`${e.position}.º lugar`}>
                        {medal(e.position)}
                      </span>
                      <MemberFace avatarUrl={e.avatarUrl} size={48} />
                      <span className="leaderboard-row__who">
                        <strong>{e.displayName}</strong>
                        {e.fullName && <small>{e.fullName}</small>}
                        <small>
                          Ensaios: {e.rehearsals} · Atuações: {e.events}
                        </small>
                      </span>
                      <span className="leaderboard-row__score">
                        <span className="member-badge member-badge--position">
                          Nível {e.level} – {e.rankName}
                        </span>
                        <strong>{e.xp} XP</strong>
                      </span>
                    </button>
                  </li>
                ))}
              </ol>
              {data.entries.length > shown && (
                <button type="button" className="btn btn--ghost btn--sm members-more" onClick={() => setShown(shown + PAGE)}>
                  Mostrar mais ({data.entries.length - shown})
                </button>
              )}
            </>
          )}

          {open && <MemberDialog id={open} fiscalYear={fiscalYear} onClose={() => setOpen(null)} />}
        </>
      )}
    </section>
  );
}

/** "Detalhes da Classificação": level and progress for the year shown, XP origin and activities of all time, comments. */
function MemberDialog({ id, fiscalYear, onClose }: { id: string; fiscalYear: string; onClose: () => void }) {
  const [detail, setDetail] = useState<MemberDetail | 'missing' | null>();

  useEffect(() => {
    api.member(id, fiscalYear).then((o) => setDetail(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : null));
  }, [id, fiscalYear]);

  return (
    <Dialog title="Detalhes da classificação" size="lg" onClose={onClose}>
      {detail === undefined ? (
        <Loading label="A carregar…" />
      ) : detail === 'missing' ? (
        <p className="form__banner" role="alert">
          Este membro já não existe.
        </p>
      ) : detail === null ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar os detalhes.
        </p>
      ) : (
        <div className="member-detail">
          <div className="member-detail__head">
            <MemberFace avatarUrl={detail.avatarUrl} size={96} />
            <p className="member-detail__name">{detail.displayName}</p>
            {detail.fullName && <p className="who__meta">{detail.fullName}</p>}
            <span className="member-badges">
              <span className="member-badge member-badge--position">Nível {detail.progress.level}</span>
              <span className="member-badge">{detail.progress.rankName}</span>
            </span>
          </div>

          <section className="member-section" aria-label="Estatísticas">
            <h3 className="member-section__title">Estatísticas</h3>
            <dl className="member-fields">
              <div className="member-field">
                <dt>XP total</dt>
                <dd>{detail.breakdown.totalXp}</dd>
              </div>
            </dl>
            {detail.breakdown.totalXp > 0 && (
              <ul className="member-rows leaderboard-xp">
                <li className="member-row">
                  <Icon name="music" />
                  <span className="member-row__who">
                    <strong>Ensaios</strong>
                    <small>
                      {detail.breakdown.rehearsalCount} × {detail.breakdown.rehearsalXpPerUnit} XP
                    </small>
                  </span>
                  <span className="member-badge">{detail.breakdown.rehearsalXpTotal} XP</span>
                </li>
                {detail.breakdown.eventsByType.length === 0 ? (
                  <li className="note">Nenhuma atuação registada</li>
                ) : (
                  detail.breakdown.eventsByType.map((t) => (
                    <li key={t.type} className="member-row">
                      <Icon name="calendar" />
                      <span className="member-row__who">
                        <strong>{t.type}</strong>
                        <small>
                          {t.count} × {t.xpPerUnit} XP
                        </small>
                      </span>
                      <span className="member-badge">{t.totalXp} XP</span>
                    </li>
                  ))
                )}
              </ul>
            )}
          </section>

          <details className="who__more" open>
            <summary>Ver atuações e ensaios detalhados</summary>
            {detail.activities.length === 0 ? (
              <p className="note">Nenhuma atividade registada</p>
            ) : (
              <ul className="member-activities">
                {detail.activities.map((a, i) => (
                  <li key={i}>
                    <Icon name={a.isRehearsal ? 'music' : 'calendar'} />
                    <span>
                      <strong>{a.name}</strong>
                      <small>
                        {a.date.split('-').reverse().join('/')} · {a.type}
                      </small>
                    </span>
                    <span className="member-badge member-badge--active">+{a.xpEarned} XP</span>
                  </li>
                ))}
              </ul>
            )}
          </details>

          <details className="who__more">
            <summary>Nível de alcoolismo</summary>
            <dl className="member-fields">
              <div className="member-field">
                <dt>Nível atual</dt>
                <dd>
                  {detail.progress.level} – {detail.progress.rankName}
                  {detail.progress.isMaxLevel && ' (nível máximo)'}
                </dd>
              </div>
              <div className="member-field">
                <dt>XP</dt>
                <dd>{detail.progress.xp}</dd>
              </div>
              {!detail.progress.isMaxLevel && (
                <div className="member-field">
                  <dt>Próximo nível</dt>
                  <dd>
                    {detail.progress.level + 1}
                    {detail.progress.nextRankName && ` – ${detail.progress.nextRankName}`}, faltam {detail.progress.xpToNextLevel} XP
                  </dd>
                </div>
              )}
            </dl>
            {!detail.progress.isMaxLevel && (
              <progress className="member-progress__bar" max={100} value={detail.progress.progressPercentage} aria-label="Progresso para o próximo nível" />
            )}
          </details>

          <Comments memberId={detail.id} />
        </div>
      )}
    </Dialog>
  );
}

function Comments({ memberId }: { memberId: string }) {
  const id = useId();
  const [list, setList] = useState<Comment[] | null>();
  const [text, setText] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [deleting, setDeleting] = useState<Comment>();

  const load = () => api.comments(memberId).then((o) => setList(o.kind === 'ok' ? o.data : null));
  useEffect(() => {
    load();
  }, [memberId]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!text.trim()) return setError('Escreva o comentário.');
    setBusy(true);
    const o = await api.comment(memberId, text);
    setBusy(false);
    if (o.kind === 'ok') {
      setList(o.data);
      setText('');
      setError(undefined);
    } else setError(problem(o, 'publicar o comentário'));
  };

  const like = async (c: Comment) => {
    const o = await api.like(c.id);
    if (o.kind === 'ok') load();
    else setError(problem(o, 'gostar do comentário'));
  };

  return (
    <section className="member-section" aria-labelledby={`${id}-title`}>
      <h3 id={`${id}-title`} className="member-section__title">
        Comentários
      </h3>
      <form className="form" onSubmit={submit} noValidate>
        <div className={error ? 'form__field form__field--error' : 'form__field'}>
          <label htmlFor={id} className="sr-only">
            Comentário
          </label>
          <textarea
            id={id}
            rows={3}
            maxLength={MAX_COMMENT}
            placeholder="Deixa um comentário…"
            value={text}
            onChange={(e) => setText(e.target.value)}
            aria-invalid={error ? true : undefined}
          />
          <p className="form__hint">
            {text.length} / {MAX_COMMENT}
          </p>
          {error && (
            <p className="form__error" role="alert">
              {error}
            </p>
          )}
        </div>
        <button type="submit" className="btn btn--primary btn--sm" disabled={busy || !text.trim()}>
          {busy && <span className="spinner spinner--small" aria-hidden="true" />}
          Publicar
        </button>
      </form>

      {list === undefined ? (
        <Loading label="A carregar os comentários…" />
      ) : list === null ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar os comentários.
        </p>
      ) : list.length === 0 ? (
        <p className="note">Ainda não há comentários.</p>
      ) : (
        <ul className="member-rows leaderboard-comments">
          {list.map((c) => (
            <li key={c.id} className="leaderboard-comment">
              <span className="leaderboard-comment__head">
                <MemberFace avatarUrl={c.authorAvatarUrl} size={36} />
                <strong>{c.authorName}</strong>
                <small className="note">{ago(c.createdAt)}</small>
                {c.canDelete && (
                  <button type="button" className="icon-btn icon-btn--danger" onClick={() => setDeleting(c)}>
                    <Icon name="trash" />
                    <span className="sr-only">Eliminar comentário</span>
                  </button>
                )}
              </span>
              <p className="leaderboard-comment__text">{c.text}</p>
              <span className="leaderboard-comment__likes">
                <button type="button" className={c.likedByMe ? 'btn btn--ghost btn--sm is-liked' : 'btn btn--ghost btn--sm'} aria-pressed={c.likedByMe} onClick={() => like(c)}>
                  <Icon name="star" />
                  {c.likedByMe ? 'Gostas' : 'Gostar'} · {c.likes}
                </button>
                {c.likedBy.length > 0 && <small className="note">{c.likedBy.join(', ')}</small>}
              </span>
            </li>
          ))}
        </ul>
      )}

      {deleting && <DeleteComment comment={deleting} onClose={() => setDeleting(undefined)} onDone={load} />}
    </section>
  );
}

function DeleteComment({ comment, onClose, onDone }: { comment: Comment; onClose: () => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const remove = async () => {
    setBusy(true);
    const o = await api.remove(comment.id);
    setBusy(false);
    if (o.kind === 'ok' || o.kind === 'notfound') {
      onDone();
      onClose();
    } else setError(problem(o, 'eliminar o comentário'));
  };

  return (
    <Dialog
      title="Eliminar comentário"
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
      <p>Eliminar o comentário de {comment.authorName}? Deixa de aparecer para todos.</p>
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
