import { useEffect, useMemo, useState } from 'react';
import { EnrollmentDialog } from '../components/EventAnswer';
import { PrizesDialog } from '../components/EventPrizesDialog';
import { WhereYouWereDialog, StatsDialog } from '../components/EventStats';
import { EventFormDialog, DeleteEventDialog, CancelEventDialog, ReactivateEventDialog, NoticeDialog } from '../components/EventManageLazy';
import { eventsApi, type EventSummary, type EventAgenda } from '../data/eventsApi';
import { Icon } from '../components/icons';
import { ActionNotice, RetryNotice } from '../components/Notice';
import { filtersFromUrl, filtersToUrl, filterPast, groupBySeason, type Filters } from '../data/eventFilters';
import { Toolbar } from '../components/EventsToolbar';
import { AgendaCard, ArchiveRow, type Manage } from '../components/EventsCards';
import { Loading } from '../components/ui/Loading';
import { PageHead } from '../components/ui/PageHead';

/**
 * Requests' "criar atuação" lands here with ?openModal=true&name=&location=&date=&description=:
 * read once, then taken out of the URL so the archive filters ignore it.
 */
function takeCreatePrefill() {
  const url = new URL(location.href);
  if (url.searchParams.get('openModal') !== 'true') return undefined;
  const p = url.searchParams;
  const date = p.get('date') ?? '';
  const prefill = {
    name: p.get('name') ?? '',
    location: p.get('location') ?? '',
    description: p.get('description') ?? '',
    ...(/^\d{4}-\d{2}-\d{2}$/.test(date) ? { date } : {}),
  };
  for (const key of ['openModal', 'name', 'location', 'date', 'description', 'type', 'requestId']) p.delete(key);
  history.replaceState(null, '', url.pathname + url.search);
  return prefill;
}

// Read once per page load, at module scope: a render React discards (Suspense) must not lose it.
let pendingPrefill = takeCreatePrefill();

/**
 * /events - the agenda. Upcoming dates first, then the archive by season with its prizes and
 * videos. Visitors see what was always public; signed-in members also see their own answer and
 * the counts, and answer in a modal (from the card or the event page). Everything is decided by GET /api/events.
 */
export default function Events() {
  const [agenda, setAgenda] = useState<EventAgenda | null>();
  const [prefill, setPrefill] = useState(() => pendingPrefill);
  const [filters, setFilters] = useState<Filters>(filtersFromUrl);
  const [answering, setAnswering] = useState<number>();
  const [editing, setEditing] = useState<number | 'new'>();
  const [deleting, setDeleting] = useState<EventSummary>();
  const [acting, setActing] = useState<{ action: 'notice' | 'cancel' | 'reactivate'; event: EventSummary }>();
  const [prizes, setPrizes] = useState(false);
  const [where, setWhere] = useState(false);
  const [stats, setStats] = useState(false);

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
  // The create form opens once the agenda says the caller may create (Admin/Owner); otherwise it is dropped.
  useEffect(() => {
    if (!prefill || !agenda) return;
    pendingPrefill = undefined;
    if (agenda.canEdit) setEditing('new');
    else setPrefill(undefined);
  }, [agenda, prefill]);
  const hasPrizes = agenda?.past.some((e) => e.trophies.length > 0);
  // The server sends canEdit (Mod and above: create, edit) and canManage (Admin/Owner: the rest) and refuses the
  // writes for anyone else. Notices and cancel / reactivate only for upcoming dates.
  const manage = (e: EventSummary): Manage =>
    agenda?.canEdit
      ? {
          onEdit: () => setEditing(e.id),
          ...(agenda.canManage
            ? {
                onDelete: () => setDeleting(e),
                onNotice: e.past || e.cancelled ? undefined : () => setActing({ action: 'notice', event: e }),
                onCancel: e.past ? undefined : () => setActing({ action: e.cancelled ? 'reactivate' : 'cancel', event: e }),
              }
            : {}),
        }
      : undefined;

  const update = (next: Partial<Filters>) => setFilters((f) => ({ ...f, ...next }));

  // The address follows the filters after a pause: history.replaceState on every keystroke is what Safari throttles
  // (100 calls per 30 s), and a state updater is no place for it anyway.
  useEffect(() => {
    const timer = setTimeout(() => filtersToUrl(filters), 250);
    return () => clearTimeout(timer);
  }, [filters]);

  const past = useMemo(() => (agenda ? filterPast(agenda.past, filters) : []), [agenda, filters]);
  const seasons = useMemo(() => groupBySeason(past), [past]);

  return (
    <section className="page wrap events-page" aria-labelledby="events-title">
      <PageHead eyebrow="Agenda" titleId="events-title" title="Atuações" lead="Onde a RTUB vai tocar a seguir e por onde já passou: serenatas, festivais, arraiais e tudo o que fica pelo meio.">
        {(hasPrizes || agenda?.isMember) && (
          <div className="events-page__actions">
            {agenda?.canEdit && (
              <button type="button" className="btn btn--primary btn--sm" onClick={() => setEditing('new')}>
                <Icon name="plus" />
                Adicionar atuação
              </button>
            )}
            {hasPrizes && (
              <button type="button" className="btn btn--prizes btn--sm" onClick={() => setPrizes(true)}>
                <Icon name="trophy" />
                Prémios
              </button>
            )}
            {agenda?.isMember && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setWhere(true)}>
                <Icon name="check" />
                Onde estiveste
              </button>
            )}
            {agenda?.isMember && (
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setStats(true)}>
                <Icon name="chart" />
                Estatísticas
              </button>
            )}
          </div>
        )}
      </PageHead>

      {agenda === undefined ? (
        <Loading label="A carregar a agenda…" />
      ) : agenda === null ? (
        <RetryNotice onRetry={() => load()}>Não conseguimos abrir a agenda neste momento.</RetryNotice>
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
              <ActionNotice label="Limpar filtros" onAction={() => update({ season: '', type: '', q: '', videos: false })}>
                Nenhuma atuação corresponde a estes filtros.
              </ActionNotice>
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
          prefill={editing === 'new' ? prefill : undefined}
          types={agenda.types}
          onClose={() => {
            setEditing(undefined);
            setPrefill(undefined);
          }}
          onSaved={() => load(true)}
        />
      )}
      {stats && <StatsDialog onClose={() => setStats(false)} />}
      {acting?.action === 'notice' && <NoticeDialog event={acting.event} onClose={() => setActing(undefined)} />}
      {acting?.action === 'cancel' && (
        <CancelEventDialog event={acting.event} onClose={() => setActing(undefined)} onDone={() => load(true)} />
      )}
      {acting?.action === 'reactivate' && (
        <ReactivateEventDialog event={acting.event} onClose={() => setActing(undefined)} onDone={() => load(true)} />
      )}
      {deleting && <DeleteEventDialog event={deleting} onClose={() => setDeleting(undefined)} onDeleted={() => load(true)} />}
      {answering !== undefined && (
        <EnrollmentDialog eventId={answering} onClose={() => setAnswering(undefined)} onSaved={() => load(true)} />
      )}
      {prizes && agenda && <PrizesDialog history={agenda.past} onClose={() => setPrizes(false)} />}
      {where && agenda && <WhereYouWereDialog past={agenda.past} onClose={() => setWhere(false)} />}
    </section>
  );
}
