import { useEffect, useId, useState } from 'react';
import { Loading, useLatest } from '../components/AppShared';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { MemberFace } from '../components/MemberDialogs';
import { MemberBadge } from '../components/MemberBadge';
import { RetryNotice, SignInNotice } from '../components/Notice';
import { type Table, api, PAGE, medal } from '../data/leaderboardApi';
import { Levels } from '../components/LeaderboardLevels';
import { MemberDialog } from '../components/LeaderboardMemberDialog';

/**
 * /leaderboard - Tabela de Classificação. Signed-in members only. Everyone's XP
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

  const latest = useLatest();
  const load = () =>
    latest(api.table(fiscalYear, q), (o) => setTable(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null));

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
      </header>

      {table === 'signin' ? (
        <SignInNotice path={portal.leaderboard}>A classificação é da área de membros.</SignInNotice>
      ) : table === null ? (
        <RetryNotice onRetry={load}>Não conseguimos abrir a classificação agora.</RetryNotice>
      ) : !data ? (
        <Loading label="A carregar a classificação…" />
      ) : (
        <>
          <Levels levels={data.levels} story={data.story} me={data.me} />

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
                        <MemberBadge kind="position">
                          Nível {e.level} – {e.rankName}
                        </MemberBadge>
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
