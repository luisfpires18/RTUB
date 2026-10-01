import { useEffect, useId, useMemo, useState } from 'react';
import { Loading } from './App';
import { EnrollmentDialog, PrizesDialog } from './EventDialogs';
import { DeleteEventDialog, EventFormDialog } from './EventManage';
import { legacy, portal } from './content';
import {
  dateLabel,
  dayOf,
  eventsApi,
  monthShort,
  timeLabel,
  yearOf,
  type EventAgenda,
  type EventSummary,
} from './eventsApi';
import { Icon } from './icons';

type Filters = { season: string; type: string; q: string; videos: boolean };

function filtersFromUrl(): Filters {
  const p = new URLSearchParams(location.search);
  return { season: p.get('season') ?? '', type: p.get('type') ?? '', q: p.get('q') ?? '', videos: p.get('videos') === '1' };
}

function filtersToUrl(f: Filters) {
  const url = new URL(location.href);
  for (const [key, value] of [['season', f.season], ['type', f.type], ['q', f.q.trim()], ['videos', f.videos ? '1' : '']]) {
    if (value) url.searchParams.set(key, value);
    else url.searchParams.delete(key);
  }
  history.replaceState(null, '', url.pathname + url.search);
}

const fold = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

/** The archive filters, all client-side: the agenda is small and already filtered by the server for the caller. */
export function filterPast(past: EventSummary[], f: Filters) {
  const q = fold(f.q.trim());
  return past.filter(
    (e) =>
      (!f.season || e.season === f.season) &&
      (!f.type || e.type === f.type) &&
      (!f.videos || e.videoCount > 0) &&
      (!q || [e.name, e.location, e.member?.description ?? ''].some((t) => fold(t).includes(q))),
  );
}

/**
 * /events - the agenda. Upcoming dates first, then the archive by season with its prizes and
 * videos. Visitors see what was always public; signed-in members also see their own answer and
 * the counts, and answer in a modal (from the card or the event page). Everything is decided by GET /api/events.
 */
export default function Events() {
  const [agenda, setAgenda] = useState<EventAgenda | null>();
  const [filters, setFilters] = useState<Filters>(filtersFromUrl);
  const [answering, setAnswering] = useState<number>();
  const [editing, setEditing] = useState<number | 'new'>();
  const [deleting, setDeleting] = useState<EventSummary>();
  const [prizes, setPrizes] = useState(false);

  useEffect(() => {
    document.title = 'Atuações · RTUB';
  }, []);

  const load = (quiet = false) => {
    if (!quiet) setAgenda(undefined);
    eventsApi.agenda().then((o) => {
      if (o.kind === 'ok') setAgenda(o.data);
      else if (!quiet) setAgenda(null);
    });
  };
  useEffect(() => load(), []);
  const hasPrizes = agenda?.past.some((e) => e.trophies.length > 0);
  // Admin/Owner only: the server sends canManage, and refuses the writes for anyone else.
  const manage = (e: EventSummary) =>
    agenda?.canManage ? { onEdit: () => setEditing(e.id), onDelete: () => setDeleting(e) } : undefined;

  const update = (next: Partial<Filters>) =>
    setFilters((f) => {
      const merged = { ...f, ...next };
      filtersToUrl(merged);
      return merged;
    });

  const past = useMemo(() => (agenda ? filterPast(agenda.past, filters) : []), [agenda, filters]);
  const seasons = useMemo(() => groupBySeason(past), [past]);

  return (
    <section className="page wrap events-page" aria-labelledby="events-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Agenda</p>
          <h1 id="events-title" className="page__title">
            Atuações
          </h1>
          <p className="page__lead">
            Onde a RTUB vai tocar a seguir e por onde já passou: serenatas, festivais, arraiais e tudo o que fica pelo
            meio.
          </p>
        </div>
        {(hasPrizes || agenda?.isMember) && (
          <div className="events-page__actions">
            {agenda?.canManage && (
              <button type="button" className="btn btn--primary btn--sm" onClick={() => setEditing('new')}>
                <Icon name="plus" />
                Adicionar atuação
              </button>
            )}
            {hasPrizes && (
              <button type="button" className="btn btn--gold btn--sm" onClick={() => setPrizes(true)}>
                <Icon name="trophy" />
                Prémios
              </button>
            )}
            {agenda?.isMember && (
              <a className="btn btn--ghost btn--sm" href={legacy.memberEvents}>
                <Icon name="arrow" />
                Área de membros
              </a>
            )}
          </div>
        )}
      </header>

      {agenda === undefined ? (
        <Loading label="A carregar a agenda…" />
      ) : agenda === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir a agenda neste momento.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => load()}>
            Tentar novamente
          </button>
        </div>
      ) : (
        <>
          <section className="events-block" aria-labelledby="upcoming-title">
            <h2 id="upcoming-title" className="events-block__title">
              Próximas datas
            </h2>
            {agenda.upcoming.length === 0 ? (
              <div className="upcoming-empty">
                <Icon name="calendar" />
                <p>Sem datas marcadas por agora. Novas atuações aparecem aqui assim que forem confirmadas.</p>
              </div>
            ) : (
              <ul className="agenda">
                {agenda.upcoming.map((e) => (
                  <li key={e.id}>
                    <AgendaCard event={e} onAnswer={() => setAnswering(e.id)} manage={manage(e)} />
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="events-block" aria-labelledby="past-title">
            <h2 id="past-title" className="events-block__title">
              Já passámos por aqui
            </h2>
            <Toolbar agenda={agenda} filters={filters} onChange={update} />
            {agenda.past.length === 0 ? (
              <p className="note">O arquivo ainda está vazio.</p>
            ) : seasons.length === 0 ? (
              <div className="notice" role="status">
                <p>Nenhuma atuação corresponde a estes filtros.</p>
                <button
                  type="button"
                  className="btn btn--ghost btn--sm"
                  onClick={() => update({ season: '', type: '', q: '', videos: false })}
                >
                  Limpar filtros
                </button>
              </div>
            ) : (
              <div className="archive">
                {seasons.map(([season, events]) => (
                  <section key={season} className="archive__season" aria-label={`Época ${season}`}>
                    <h3 className="archive__label">
                      {season.replace('-', '–')}
                      <span>{events.length === 1 ? '1 atuação' : `${events.length} atuações`}</span>
                    </h3>
                    <ul className="archive__list">
                      {events.map((e) => (
                        <li key={e.id}>
                          <ArchiveRow event={e} manage={manage(e)} />
                        </li>
                      ))}
                    </ul>
                  </section>
                ))}
              </div>
            )}
          </section>
        </>
      )}
      {editing !== undefined && agenda?.types && (
        <EventFormDialog
          eventId={editing === 'new' ? undefined : editing}
          types={agenda.types}
          onClose={() => setEditing(undefined)}
          onSaved={() => load(true)}
        />
      )}
      {deleting && <DeleteEventDialog event={deleting} onClose={() => setDeleting(undefined)} onDeleted={() => load(true)} />}
      {answering !== undefined && (
        <EnrollmentDialog eventId={answering} onClose={() => setAnswering(undefined)} onSaved={() => load(true)} />
      )}
      {prizes && agenda && <PrizesDialog history={agenda.past} onClose={() => setPrizes(false)} />}
    </section>
  );
}

function groupBySeason(events: EventSummary[]) {
  const groups: [string, EventSummary[]][] = [];
  for (const e of events) {
    const last = groups[groups.length - 1];
    if (last?.[0] === e.season) last[1].push(e);
    else groups.push([e.season, [e]]);
  }
  return groups;
}

function Toolbar({
  agenda,
  filters,
  onChange,
}: {
  agenda: EventAgenda;
  filters: Filters;
  onChange: (next: Partial<Filters>) => void;
}) {
  const ids = { q: useId(), season: useId(), type: useId(), videos: useId() };
  const seasons = useMemo(() => [...new Set(agenda.past.map((e) => e.season))], [agenda]);
  const types = useMemo(() => [...new Set(agenda.past.map((e) => e.type))].sort((a, b) => a.localeCompare(b, 'pt')), [agenda]);

  return (
    <div className="events-tools" role="search">
      <label className="control" htmlFor={ids.q}>
        <span className="sr-only">Procurar no arquivo</span>
        <Icon name="search" />
        <input
          id={ids.q}
          type="search"
          placeholder="Procurar por nome ou local"
          value={filters.q}
          onChange={(e) => onChange({ q: e.target.value })}
        />
      </label>
      <label className="control control--select" htmlFor={ids.season}>
        <span className="sr-only">Época</span>
        <select id={ids.season} value={filters.season} onChange={(e) => onChange({ season: e.target.value })}>
          <option value="">Todas as épocas</option>
          {seasons.map((s) => (
            <option key={s} value={s}>
              {s.replace('-', '–')}
            </option>
          ))}
        </select>
      </label>
      <label className="control control--select" htmlFor={ids.type}>
        <span className="sr-only">Tipo</span>
        <select id={ids.type} value={filters.type} onChange={(e) => onChange({ type: e.target.value })}>
          <option value="">Todos os tipos</option>
          {types.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
      </label>
      <label className="check" htmlFor={ids.videos}>
        <input id={ids.videos} type="checkbox" checked={filters.videos} onChange={(e) => onChange({ videos: e.target.checked })} />
        Só com vídeos
      </label>
    </div>
  );
}

/** The caller's own answer, for members only. */
export function MyStatus({ event }: { event: EventSummary }) {
  const status = event.member?.myStatus;
  if (!status || event.cancelled) return null;
  const going = status === 'going';
  const label = event.past ? (going ? 'Foste' : 'Não foste') : going ? 'Vais' : 'Não vais';
  return (
    <span className={going ? 'pill pill--yes' : 'pill pill--no'}>
      <Icon name={going ? 'check' : 'close'} />
      {label}
    </span>
  );
}

/**
 * One upcoming date. The whole card opens the event (a stretched link); a signed-in member also gets a
 * quick reply that opens the answer modal without leaving the agenda.
 */
type Manage = { onEdit: () => void; onDelete: () => void } | undefined;

/** Edit and delete, over the card's stretched link; Admin/Owner only. */
function ManageButtons({ event, manage }: { event: EventSummary; manage: Manage }) {
  if (!manage) return null;
  return (
    <span className="manage">
      <button type="button" className="icon-btn icon-btn--sm" onClick={manage.onEdit}>
        <Icon name="pencil" />
        <span className="sr-only">Editar {event.name}</span>
      </button>
      <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={manage.onDelete}>
        <Icon name="trash" />
        <span className="sr-only">Apagar {event.name}</span>
      </button>
    </span>
  );
}

function AgendaCard({ event, onAnswer, manage }: { event: EventSummary; onAnswer: () => void; manage: Manage }) {
  const time = timeLabel(event.time);
  const m = event.member;
  const canAnswer = m !== null && !event.cancelled;
  return (
    <article className={event.cancelled ? 'agenda-card agenda-card--cancelled' : 'agenda-card'}>
      <time className="agenda-card__date" dateTime={event.date}>
        <span className="agenda-card__day">{dayOf(event.date)}</span>
        <span className="agenda-card__month">{monthShort(event.date)}</span>
        <span className="agenda-card__year">{yearOf(event.date)}</span>
      </time>
      <div className="agenda-card__body">
        <div className="agenda-card__tags">
          <span className="tag">{event.type}</span>
          {event.cancelled && <span className="tag tag--cancelled">Cancelada</span>}
          <ManageButtons event={event} manage={manage} />
        </div>
        <h3 className="agenda-card__name">
          <a className="agenda-card__link" href={portal.event(event.id)}>
            {event.name}
          </a>
        </h3>
        <p className="agenda-card__meta">
          <Icon name="clock" />
          <span>
            {dateLabel(event)}
            {time && ` · ${time}`}
          </span>
        </p>
        <p className="agenda-card__meta">
          <Icon name="geo" />
          <span>{event.location}</span>
        </p>
        {canAnswer && (
          <div className="agenda-card__foot">
            <span className="agenda-card__count">
              {m.goingCount === 1 ? '1 confirmado' : `${m.goingCount} confirmados`}
            </span>
            <button
              type="button"
              className={`quick-reply quick-reply--${m.myStatus ?? 'none'}`}
              onClick={onAnswer}
              aria-label={`${m.myStatus ? 'Alterar resposta' : 'Responder'}: ${event.name}`}
            >
              {m.myStatus === 'going' ? (
                <>
                  <Icon name="check" />
                  Vais
                </>
              ) : m.myStatus === 'notGoing' ? (
                <>
                  <Icon name="close" />
                  Não vais
                </>
              ) : (
                'Responder'
              )}
            </button>
          </div>
        )}
      </div>
      <Icon name="arrow" className="agenda-card__go" />
    </article>
  );
}

/** One past event; the whole row opens it (a stretched link), with Admin/Owner tools on top. */
function ArchiveRow({ event, manage }: { event: EventSummary; manage: Manage }) {
  return (
    <div className={event.cancelled ? 'archive-row archive-row--cancelled' : 'archive-row'}>
      <time className="archive-row__date" dateTime={event.date}>
        <span>{dayOf(event.date)}</span> {monthShort(event.date)}
      </time>
      <span className="archive-row__main">
        <a className="archive-row__name" href={portal.event(event.id)}>
          {event.name}
        </a>
        <span className="archive-row__meta">
          {event.location} · {event.type}
          {event.cancelled && ' · cancelada'}
        </span>
      </span>
      <span className="archive-row__badges">
        <MyStatus event={event} />
        {event.trophies.length > 0 && (
          <span className="badge" title="Prémios">
            <Icon name="trophy" />
            {event.trophies.length}
          </span>
        )}
        {event.videoCount > 0 && (
          <span className="badge" title="Vídeos">
            <Icon name="video" />
            {event.videoCount}
          </span>
        )}
        <ManageButtons event={event} manage={manage} />
      </span>
    </div>
  );
}
