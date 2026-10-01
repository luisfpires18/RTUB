import { useEffect, useId, useMemo, useState } from 'react';
import { Loading } from './App';
import { portal } from './content';
import { EnrollmentDialog } from './EventDialogs';
import { dateLabel, eventsApi, timeLabel, type EventAgenda, type EventSummary } from './eventsApi';
import { Icon } from './icons';
import { loginTo } from './musicApi';

const RECENT = 10;

const fold = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

/**
 * /events/my-enrollments - "As minhas inscrições" (React track 012E), signed-in members only. The next
 * dates with the member's own answer (answered in the usual modal), then the archive as the old Blazor
 * "Minhas Inscrições" showed it: the last 10 (or every) past, not cancelled event, "Foste" / "Não foste"
 * (no answer counts as not going, as before) and the share of them the member went to. Everything comes
 * from GET /api/events, which already carries the caller's own answer; nobody else's is shown.
 */
export default function MyEnrollments() {
  const [agenda, setAgenda] = useState<EventAgenda | null>();
  const [answering, setAnswering] = useState<number>();
  const [all, setAll] = useState(false);
  const [q, setQ] = useState('');
  const ids = { q: useId(), all: useId() };

  useEffect(() => {
    document.title = 'As minhas inscrições · RTUB';
  }, []);

  const load = (quiet = false) => {
    if (!quiet) setAgenda(undefined);
    eventsApi.agenda().then((o) => {
      if (o.kind === 'ok') setAgenda(o.data);
      else if (!quiet) setAgenda(null);
    });
  };
  useEffect(() => load(), []);

  const upcoming = useMemo(() => agenda?.upcoming.filter((e) => !e.cancelled) ?? [], [agenda]);
  const past = useMemo(() => {
    const done = agenda?.past.filter((e) => !e.cancelled) ?? [];
    return all ? done : done.slice(0, RECENT);
  }, [agenda, all]);
  const went = past.filter((e) => e.member?.myStatus === 'going').length;
  const shown = past.filter((e) => !q.trim() || fold(e.name).includes(fold(q.trim())));

  return (
    <section className="page wrap my-enrollments" aria-labelledby="my-enrollments-title">
      <a className="back-link" href={portal.events}>
        <Icon name="arrow" />
        Agenda
      </a>
      <header className="page__head">
        <p className="eyebrow">Área de membros</p>
        <h1 id="my-enrollments-title" className="page__title">
          As minhas inscrições
        </h1>
      </header>

      {agenda === undefined ? (
        <Loading label="A carregar as tuas inscrições…" />
      ) : agenda === null ? (
        <div className="notice" role="status">
          <p>Não conseguimos abrir as tuas inscrições agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => load()}>
            Tentar novamente
          </button>
        </div>
      ) : !agenda.isMember ? (
        <div className="notice" role="status">
          <p>As inscrições são da área de membros.</p>
          <a className="btn btn--primary btn--sm" href={loginTo(portal.myEnrollments)}>
            <Icon name="login" />
            Entrar
          </a>
        </div>
      ) : (
        <>
          <section className="events-block" aria-labelledby="my-next-title">
            <h2 id="my-next-title" className="events-block__title">
              Próximas datas
            </h2>
            {upcoming.length === 0 ? (
              <p className="note">Sem datas marcadas por agora.</p>
            ) : (
              <ul className="my-list">
                {upcoming.map((e) => (
                  <li key={e.id}>
                    <Row event={e}>
                      <button
                        type="button"
                        className={`quick-reply quick-reply--${e.member?.myStatus ?? 'none'}`}
                        onClick={() => setAnswering(e.id)}
                        aria-label={`${e.member?.myStatus ? 'Alterar resposta' : 'Responder'}: ${e.name}`}
                      >
                        {e.member?.myStatus === 'going' ? (
                          <>
                            <Icon name="check" />
                            Vais
                          </>
                        ) : e.member?.myStatus === 'notGoing' ? (
                          <>
                            <Icon name="close" />
                            Não vais
                          </>
                        ) : (
                          'Responder'
                        )}
                      </button>
                    </Row>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="events-block" aria-labelledby="my-past-title">
            <h2 id="my-past-title" className="events-block__title">
              Onde estiveste
            </h2>
            {past.length > 0 && (
              <p className="my-enrollments__rate">
                Foste a <strong>{went}</strong> de {past.length} {all ? 'atuações' : `atuações (as últimas ${RECENT})`}
                {' · '}
                {(went / past.length).toLocaleString('pt-PT', { style: 'percent', maximumFractionDigits: 1 })}
              </p>
            )}
            <div className="events-tools" role="search">
              <label className="control" htmlFor={ids.q}>
                <span className="sr-only">Procurar atuação</span>
                <Icon name="search" />
                <input id={ids.q} type="search" placeholder="Procurar pelo nome" value={q} onChange={(e) => setQ(e.target.value)} />
              </label>
              <label className="check" htmlFor={ids.all}>
                <input id={ids.all} type="checkbox" checked={all} onChange={(e) => setAll(e.target.checked)} />
                Mostrar todas
              </label>
            </div>
            {shown.length === 0 ? (
              <p className="note">{past.length === 0 ? 'Ainda sem atuações no arquivo.' : 'Nenhuma atuação com esse nome.'}</p>
            ) : (
              <ul className="my-list">
                {shown.map((e) => (
                  <li key={e.id}>
                    <Row event={e}>
                      {e.member?.myStatus === 'going' ? (
                        <span className="pill pill--yes">
                          <Icon name="check" />
                          Foste
                        </span>
                      ) : (
                        <span className="pill pill--no">
                          <Icon name="close" />
                          Não foste
                        </span>
                      )}
                    </Row>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}

      {answering !== undefined && <EnrollmentDialog eventId={answering} onClose={() => setAnswering(undefined)} onSaved={() => load(true)} />}
    </section>
  );
}

/** One event: date, name (to its page) and place, with the answer on the right. */
function Row({ event, children }: { event: EventSummary; children: React.ReactNode }) {
  const time = timeLabel(event.time);
  return (
    <div className="my-row">
      <span className="my-row__main">
        <a className="my-row__name" href={portal.event(event.id)}>
          {event.name}
        </a>
        <span className="my-row__meta">
          {dateLabel(event)}
          {time && ` · ${time}`} · {event.location}
        </span>
      </span>
      {children}
    </div>
  );
}
