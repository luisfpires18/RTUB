import { useEffect, useId, useState } from 'react';
import { LoadMore } from '../components/ui/LoadMore';
import { ToolsBar, SearchField, SelectField } from '../components/ui/SearchTools';
import { EmptyNote } from '../components/ui/EmptyNote';
import { PageHead } from '../components/ui/PageHead';
import { useLatest } from '../components/AppShared';
import { portal } from '../data/content';
import { MemberBadge } from '../components/MemberBadge';
import { RetryNotice, SignInNotice } from '../components/Notice';
import { type Table, api, PAGE, medal } from '../data/leaderboardApi';
import { Levels } from '../components/LeaderboardLevels';
import { MemberDialog } from '../components/LeaderboardMemberDialog';
import { Loading } from '../components/ui/Loading';
import { Avatar } from '../components/ui/Avatar';

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
      <PageHead eyebrow="Área de membros" title="Tabela de Classificação" titleId="leaderboard-title" lead="Cada ensaio e cada atuação contam XP; o XP sobe o nível." />

      {table === 'signin' ? (
        <SignInNotice path={portal.leaderboard}>A classificação é da área de membros.</SignInNotice>
      ) : table === null ? (
        <RetryNotice onRetry={load}>Não conseguimos abrir a classificação agora.</RetryNotice>
      ) : !data ? (
        <Loading label="A carregar a classificação…" />
      ) : (
        <>
          <Levels levels={data.levels} story={data.story} me={data.me} />

          <ToolsBar>
            <SearchField id={ids.q} label="Procurar na classificação" placeholder="Procurar por nome, alcunha ou nível" value={text} onChange={setText} />
            <SelectField id={ids.year} label="Ano letivo" value={fiscalYear} onChange={setFiscalYear}>
              <option value="">Todos os anos</option>
              {data.fiscalYears.map((y) => (
                <option key={y.value} value={y.value}>
                  {y.label}
                </option>
              ))}
            </SelectField>
          </ToolsBar>

          {data.entries.length === 0 ? (
            <EmptyNote>{data.total === 0 ? 'Ainda não há membros classificados.' : 'Ninguém na classificação corresponde à pesquisa.'}</EmptyNote>
          ) : (
            <>
              <ol className="leaderboard-rows" aria-label="Classificação">
                {data.entries.slice(0, shown).map((e) => (
                  <li key={e.id}>
                    <button type="button" className={`leaderboard-row${e.position <= 3 ? ` leaderboard-row--top${e.position}` : ''}`} onClick={() => setOpen(e.id)}>
                      <span className="leaderboard-row__pos" aria-label={`${e.position}.º lugar`}>
                        {medal(e.position)}
                      </span>
                      <Avatar className="who__avatar" src={e.avatarUrl} size={48} />
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
              <LoadMore remaining={data.entries.length - shown} onMore={() => setShown(shown + PAGE)} />
            </>
          )}

          {open && <MemberDialog id={open} fiscalYear={fiscalYear} onClose={() => setOpen(null)} />}
        </>
      )}
    </section>
  );
}
