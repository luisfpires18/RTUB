import { useEffect, useId, useMemo, useState } from 'react';
import { Loading } from './App';
import { dateLabel, dayOf, monthShort, weekday } from './eventsApi';
import { Icon } from './icons';
import { loginTo } from './musicApi';
import { MyPresencesDialog, PresenceDialog, PresencePill, RangeDialog, RehearsalFormDialog, StatsDialog } from './RehearsalDialogs';
import { rehearsalsApi, type RehearsalAgenda, type RehearsalSummary } from './rehearsalsApi';

const fold = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

/** The season (September-August) a day falls in, as the server names it. */
const seasonOf = (d: Date) => {
  const start = d.getMonth() >= 8 ? d.getFullYear() : d.getFullYear() - 1;
  return `${start}-${start + 1}`;
};

function filtersFromUrl() {
  const p = new URLSearchParams(location.search);
  // ?fy= was the Blazor page's season filter; old links keep working.
  return { season: p.get('season') ?? p.get('fy') ?? seasonOf(new Date()), q: p.get('q') ?? '' };
}

/**
 * /rehearsals - Ensaios (React track 014; was a Blazor page). Signed-in members only. The next rehearsals with the
 * member's presença and a quick reply, then the past ones by season. Rehearsals speak of presenças; a "vou" stays
 * pending until Admin/Owner confirms it after the rehearsal. Admin/Owner add rehearsals here and manage each on its page.
 */
export default function Rehearsals() {
  const [agenda, setAgenda] = useState<RehearsalAgenda | 'signin' | null>();
  const [filters, setFilters] = useState(filtersFromUrl);
  const [answering, setAnswering] = useState<number>();
  const [dialog, setDialog] = useState<'new' | 'range' | 'stats' | 'mine'>();
  const ids = { season: useId(), q: useId() };

  useEffect(() => {
    document.title = 'Ensaios · RTUB';
  }, []);

  const load = (quiet = false) => {
    if (!quiet) setAgenda(undefined);
    rehearsalsApi.agenda().then((o) => {
      if (o.kind === 'ok') setAgenda(o.data);
      else if (!quiet) setAgenda(o.kind === 'signin' ? 'signin' : null);
    });
  };
  useEffect(() => load(), []);

  const update = (next: Partial<typeof filters>) =>
    setFilters((f) => {
      const merged = { ...f, ...next };
      const url = new URL(location.href);
      url.searchParams.delete('fy');
      url.searchParams.set('season', merged.season);
      if (merged.q.trim()) url.searchParams.set('q', merged.q.trim());
      else url.searchParams.delete('q');
      history.replaceState(null, '', url.pathname + url.search);
      return merged;
    });

  const data = agenda && agenda !== 'signin' ? agenda : null;
  const seasons = useMemo(() => [...new Set(data?.past.map((r) => r.season) ?? [])], [data]);
  const past = useMemo(() => {
    const q = fold(filters.q.trim());
    return (data?.past ?? []).filter(
      (r) => (!filters.season || r.season === filters.season) && (!q || [r.theme ?? '', r.location, r.description ?? ''].some((t) => fold(t).includes(q))),
    );
  }, [data, filters]);

  return (
    <section className="page wrap events-page rehearsals-page" aria-labelledby="rehearsals-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="rehearsals-title" className="page__title">
            Ensaios
          </h1>
          <p className="page__lead">Às terças e quintas, das 21:30 à meia-noite. Marca a tua presença antes de cada ensaio.</p>
        </div>
        {data && (
          <div className="events-page__actions">
            {data.canManage && (
              <>
                <button type="button" className="btn btn--primary btn--sm" onClick={() => setDialog('new')}>
                  <Icon name="plus" />
                  Adicionar ensaio
                </button>
                <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('range')}>
                  <Icon name="calendar" />
                  Vários
                </button>
              </>
            )}
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('mine')}>
              <Icon name="check" />
              As minhas presenças
            </button>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDialog('stats')}>
              <Icon name="chart" />
              Estatísticas
            </button>
          </div>
        )}
      </header>

      {agenda === undefined ? (
        <Loading label="A carregar os ensaios…" />
      ) : agenda === 'signin' ? (
        <div className="notice" role="status">
          <p>Os ensaios são da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo('/rehearsals')}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : agenda === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir os ensaios agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => load()}>
            Tentar novamente
          </button>
        </div>
      ) : (
        <>
          <section className="events-block" aria-labelledby="next-title">
            <h2 id="next-title" className="events-block__title">
              Próximos ensaios
            </h2>
            {agenda.upcoming.length === 0 ? (
              <div className="upcoming-empty">
                <Icon name="calendar" />
                <p>Sem ensaios marcados por agora.</p>
              </div>
            ) : (
              <ul className="agenda">
                {agenda.upcoming.map((r) => (
                  <li key={r.id}>
                    <RehearsalCard rehearsal={r} onAnswer={() => setAnswering(r.id)} />
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="events-block" aria-labelledby="past-title">
            <h2 id="past-title" className="events-block__title">
              Ensaios anteriores
            </h2>
            <div className="events-tools" role="search">
              <label className="control" htmlFor={ids.q}>
                <span className="sr-only">Procurar ensaio</span>
                <Icon name="search" />
                <input id={ids.q} type="search" placeholder="Procurar por tema ou local" value={filters.q} onChange={(e) => update({ q: e.target.value })} />
              </label>
              <label className="control control--select" htmlFor={ids.season}>
                <span className="sr-only">Época</span>
                <select id={ids.season} value={filters.season} onChange={(e) => update({ season: e.target.value })}>
                  <option value="">Todas as épocas</option>
                  {seasons.includes(filters.season) || !filters.season ? null : <option value={filters.season}>{filters.season}</option>}
                  {seasons.map((s) => (
                    <option key={s} value={s}>
                      {s}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            {past.length === 0 ? (
              <p className="note">Nenhum ensaio anterior encontrado.</p>
            ) : (
              <ul className="archive__list">
                {past.map((r) => (
                  <li key={r.id}>
                    <PastRow rehearsal={r} />
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}

      {answering !== undefined && <PresenceDialog rehearsalId={answering} onClose={() => setAnswering(undefined)} onSaved={() => load(true)} />}
      {dialog === 'new' && <RehearsalFormDialog onClose={() => setDialog(undefined)} onSaved={() => load(true)} />}
      {dialog === 'range' && <RangeDialog onClose={() => setDialog(undefined)} onSaved={() => load(true)} />}
      {dialog === 'stats' && <StatsDialog onClose={() => setDialog(undefined)} />}
      {dialog === 'mine' && data && <MyPresencesDialog past={data.past} onClose={() => setDialog(undefined)} />}
    </section>
  );
}

/** One upcoming rehearsal: the whole card opens it; the quick reply opens the presença modal in place. */
function RehearsalCard({ rehearsal: r, onAnswer }: { rehearsal: RehearsalSummary; onAnswer: () => void }) {
  const status = r.mine?.status;
  return (
    <article className={r.cancelled ? 'agenda-card agenda-card--cancelled' : 'agenda-card'}>
      <time className="agenda-card__date" dateTime={r.date}>
        <span className="agenda-card__day">{dayOf(r.date)}</span>
        <span className="agenda-card__month">{monthShort(r.date)}</span>
        <span className="agenda-card__year">{weekday(r.date).slice(0, 3)}</span>
      </time>
      <div className="agenda-card__body">
        <div className="agenda-card__tags">
          <span className="tag">Ensaio</span>
          {r.cancelled && <span className="tag tag--cancelled">Cancelado</span>}
        </div>
        <h3 className="agenda-card__name">
          <a className="agenda-card__link" href={`/rehearsals/${r.id}`}>
            {r.theme ?? dateLabel({ date: r.date, endDate: null })}
          </a>
        </h3>
        <p className="agenda-card__meta">
          <Icon name="clock" />
          <span>
            {r.theme && `${dateLabel({ date: r.date, endDate: null })} · `}
            {r.start}–{r.end}
          </span>
        </p>
        <p className="agenda-card__meta">
          <Icon name="geo" />
          <span>{r.location}</span>
        </p>
        {!r.cancelled && (
          <div className="agenda-card__foot">
            <span className="agenda-card__count">{r.goingCount === 1 ? '1 vai' : `${r.goingCount} vão`}</span>
            <button
              type="button"
              className={`quick-reply quick-reply--${status === 'notGoing' ? 'notGoing' : status ? 'going' : 'none'}`}
              onClick={onAnswer}
              aria-label={`${status ? 'Alterar presença' : 'Marcar presença'}: ensaio de ${dateLabel({ date: r.date, endDate: null })}`}
            >
              {status === 'notGoing' ? (
                <>
                  <Icon name="close" />
                  Não vais
                </>
              ) : status ? (
                <>
                  <Icon name="check" />
                  Vais
                </>
              ) : (
                'Marcar presença'
              )}
            </button>
          </div>
        )}
      </div>
      <Icon name="arrow" className="agenda-card__go" />
    </article>
  );
}

function PastRow({ rehearsal: r }: { rehearsal: RehearsalSummary }) {
  return (
    <div className={r.cancelled ? 'archive-row archive-row--cancelled' : 'archive-row'}>
      <time className="archive-row__date" dateTime={r.date}>
        <span>{dayOf(r.date)}</span> {monthShort(r.date)}
      </time>
      <span className="archive-row__main">
        <a className="archive-row__name" href={`/rehearsals/${r.id}`}>
          {r.theme ?? `Ensaio de ${weekday(r.date)}`}
        </a>
        <span className="archive-row__meta">
          {r.location} · {r.goingCount === 1 ? '1 presença' : `${r.goingCount} presenças`}
          {r.cancelled && ' · cancelado'}
        </span>
      </span>
      <span className="archive-row__badges">
        <PresencePill rehearsal={r} />
        {r.pendingCount > 0 && (
          <span className="pill pill--wait" title="Presenças por confirmar">
            <Icon name="clock" />
            {r.pendingCount} por confirmar
          </span>
        )}
      </span>
    </div>
  );
}
