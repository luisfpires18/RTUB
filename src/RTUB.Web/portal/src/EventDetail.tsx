import { useEffect, useRef, useState } from 'react';
import { Loading } from './App';
import { legacy, portal } from './content';
import { EnrollmentDialog, PrizesDialog } from './EventDialogs';
import { MyStatus } from './Events';
import { dateLabel, eventsApi, timeLabel, type EventDetail as Detail, type EventParticipant, type EventVideo } from './eventsApi';
import { Icon } from './icons';

/**
 * ?respond=1 (from the members' Blazor page or an old link) opens the answer modal once. Read
 * without side effects: React may render and discard a first attempt (lazy pages), so the URL is
 * only cleaned once the modal actually opens.
 */
const wantsToRespond = () => new URLSearchParams(location.search).get('respond') === '1';

function dropRespondFlag() {
  const url = new URL(location.href);
  url.searchParams.delete('respond');
  history.replaceState(null, '', url.pathname + url.search);
}

/**
 * /events/{id} - one event. Visitors get the public facts, prizes and videos; signed-in members
 * also get the description, their own answer (a modal, never another page), the counts, the
 * repertoire and who is going, all on this page.
 */
export default function EventDetail({ eventId }: { eventId: number }) {
  const [detail, setDetail] = useState<Detail | 'missing' | null>();
  const [answering, setAnswering] = useState(false);
  const [prizes, setPrizes] = useState(false);
  const respond = useRef(wantsToRespond());

  const load = (quiet = false) => {
    if (!quiet) setDetail(undefined);
    eventsApi.event(eventId).then((o) => setDetail(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : null));
  };
  useEffect(() => load(), [eventId]);

  useEffect(() => {
    document.title = detail && detail !== 'missing' ? `${detail.event.name} · Atuações · RTUB` : 'Atuações · RTUB';
    if (detail && detail !== 'missing' && respond.current) {
      respond.current = false;
      dropRespondFlag();
      setAnswering(true);
    }
  }, [detail]);

  const loaded = detail && detail !== 'missing' ? detail : null;

  return (
    <section className="page wrap event-page" aria-labelledby="event-title">
      <div className="event-topbar">
        <a className="back-link" href={portal.events}>
          <Icon name="arrow" />
          Agenda
        </a>
        {loaded && loaded.event.trophies.length > 0 && (
          <button type="button" className="btn btn--gold btn--sm" onClick={() => setPrizes(true)}>
            <Icon name="trophy" />
            Prémios
          </button>
        )}
      </div>
      {detail === undefined ? (
        <Loading label="A carregar a atuação…" />
      ) : detail === 'missing' ? (
        <div className="notice" role="status">
          <p id="event-title">Esta atuação não existe ou foi removida.</p>
          <a className="btn btn--ghost btn--sm" href={portal.events}>
            Ver a agenda
          </a>
        </div>
      ) : detail === null ? (
        <div className="notice" role="status">
          <p id="event-title">Não conseguimos abrir esta atuação agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => load()}>
            Tentar novamente
          </button>
        </div>
      ) : (
        <Body detail={detail} onAnswer={() => setAnswering(true)} />
      )}
      {answering && <EnrollmentDialog eventId={eventId} onClose={() => setAnswering(false)} onSaved={() => load(true)} />}
      {prizes && loaded && <PrizesDialog current={loaded.event} onClose={() => setPrizes(false)} />}
    </section>
  );
}

function Body({ detail, onAnswer }: { detail: Detail; onAnswer: () => void }) {
  const { event, member } = detail;
  const time = timeLabel(event.time);

  return (
    <>
      <header className={event.imageUrl ? 'event-hero event-hero--image' : 'event-hero'}>
        {event.imageUrl && <img className="event-hero__image" src={event.imageUrl} alt="" />}
        <div className="event-hero__text">
          <div className="agenda-card__tags">
            <span className="tag">{event.type}</span>
            {event.cancelled && <span className="tag tag--cancelled">Cancelada</span>}
            {!event.past && !event.cancelled && <span className="tag tag--soon">Próxima</span>}
            <MyStatus event={event} />
          </div>
          <h1 id="event-title" className="page__title event-hero__title">
            {event.name}
          </h1>
          <ul className="event-facts">
            <li>
              <Icon name="calendar" />
              <span>
                {dateLabel(event)}
                {time && ` · ${time}`}
              </span>
            </li>
            <li>
              <Icon name="geo" />
              <span>{event.location}</span>
            </li>
          </ul>
        </div>
      </header>

      {event.cancelled && (
        <div className="warning event-cancelled" role="note">
          <Icon name="warning" />
          <p>
            Esta atuação foi cancelada.
            {member?.cancellationReason && <span className="event-cancelled__reason"> {member.cancellationReason}</span>}
          </p>
        </div>
      )}

      <div className={detail.isMember ? 'event-layout' : 'event-layout event-layout--public'}>
        <div className="event-main">
          {event.member?.description && (
            <section className="event-section" aria-labelledby="about-title">
              <h2 id="about-title" className="event-section__title">
                Sobre esta atuação
              </h2>
              <p className="event-description">{event.member.description}</p>
            </section>
          )}

          {member && member.repertoire.length > 0 && (
            <section className="event-section" aria-labelledby="repertoire-title">
              <h2 id="repertoire-title" className="event-section__title">
                Repertório
              </h2>
              {member.repertoire.map((day) => (
                <div key={day.date} className="repertoire-day">
                  {member.repertoire.length > 1 && <h3 className="repertoire-day__date">{dateLabel({ date: day.date, endDate: null })}</h3>}
                  <ol className="repertoire">
                    {day.songs.map((song, i) => (
                      <li key={i}>{song}</li>
                    ))}
                  </ol>
                </div>
              ))}
            </section>
          )}

          {member && !event.cancelled && <WhoIsGoing participants={member.participants} past={event.past} />}

          {detail.videos.length > 0 && <Videos videos={detail.videos} />}

          {!detail.isMember && detail.videos.length === 0 && (
            <p className="note">{event.past ? 'Ainda não há vídeos desta atuação.' : 'Mais novidades nas redes da RTUB.'}</p>
          )}
        </div>

        {detail.isMember && event.member && <MemberPanel detail={detail} onAnswer={onAnswer} />}
      </div>
    </>
  );
}

function MemberPanel({ detail, onAnswer }: { detail: Detail; onAnswer: () => void }) {
  const { event } = detail;
  const m = event.member!;
  const open = !event.past && !event.cancelled;
  const answer = m.myStatus === 'going' ? 'Disseste que vais.' : m.myStatus === 'notGoing' ? 'Disseste que não vais.' : 'Ainda não respondeste.';

  return (
    <aside className="member-panel" aria-labelledby="member-panel-title">
      <h2 id="member-panel-title" className="member-panel__title">
        Para membros
      </h2>
      {open ? (
        <>
          <p className="member-panel__answer">{answer}</p>
          <button type="button" className="btn btn--primary" onClick={onAnswer}>
            <Icon name="check" />
            {m.myStatus ? 'Alterar resposta' : 'Responder'}
          </button>
        </>
      ) : event.past && m.myStatus === 'going' && !event.cancelled ? (
        <button type="button" className="btn btn--ghost btn--sm" onClick={onAnswer}>
          A minha inscrição
        </button>
      ) : null}
      <dl className="member-panel__counts">
        <div>
          <dt>{event.past ? 'Foram' : 'Vão'}</dt>
          <dd>{m.goingCount}</dd>
        </div>
        <div>
          <dt>{event.past ? 'Não foram' : 'Não vão'}</dt>
          <dd>{detail.member?.notGoingCount ?? 0}</dd>
        </div>
        <div>
          <dt>Músicas</dt>
          <dd>{m.repertoireCount}</dd>
        </div>
      </dl>
      <ul className="member-panel__links">
        {!event.cancelled && (
          <li>
            <a href="#who-title">
              <Icon name="person" />
              {whoLabel(event.past)}
            </a>
          </li>
        )}
        <li>
          <a href={legacy.eventDiscussion(event.id)}>
            <Icon name="envelope" />
            Discussão{m.discussionCount > 0 && ` (${m.discussionCount})`}
          </a>
        </li>
      </ul>
    </aside>
  );
}

/** "Quem vai" before and during an event, "Quem foi" once it is over. */
export const whoLabel = (past: boolean) => (past ? 'Quem foi' : 'Quem vai');

/** Who answered, on this page: members going, Leitões going, and (folded) who is not going. */
function WhoIsGoing({
  participants,
  past,
}: {
  participants: { going: EventParticipant[]; leitoes: EventParticipant[]; notGoing: EventParticipant[] };
  past: boolean;
}) {
  const { going, leitoes, notGoing } = participants;
  const total = going.length + leitoes.length;

  return (
    <section className="event-section" aria-labelledby="who-title">
      <h2 id="who-title" className="event-section__title">
        {whoLabel(past)} <span className="event-section__count">{total}</span>
      </h2>
      {total === 0 && notGoing.length === 0 ? (
        <p className="note">Ainda ninguém respondeu.</p>
      ) : (
        <>
          {going.length > 0 && <People people={going} />}
          {leitoes.length > 0 && (
            <>
              <h3 className="who__group">Leitões · {leitoes.length}</h3>
              <People people={leitoes} />
            </>
          )}
          {total === 0 && <p className="note">{past ? 'Ninguém confirmou presença.' : 'Por agora ninguém confirmou.'}</p>}
          {notGoing.length > 0 && (
            <details className="who__more">
              <summary>
                {past ? 'Não foram' : 'Não vão'} · {notGoing.length}
              </summary>
              <People people={notGoing} />
            </details>
          )}
        </>
      )}
    </section>
  );
}

/** Avatar-led tiles: the face first, then the nickname, the category and what they play. */
function People({ people }: { people: EventParticipant[] }) {
  return (
    <ul className="who">
      {people.map((p, i) => (
        <li key={i} className="who__person" title={p.fullName ?? undefined}>
          <img className="who__avatar" src={p.avatarUrl} alt="" loading="lazy" width="72" height="72" />
          <p className="who__name">{p.name}</p>
          {p.badge && <span className="who__badge">{p.badge}</span>}
          {p.instrument && <p className="who__meta">{p.instrument}</p>}
          {p.notes && <p className="who__note">“{p.notes}”</p>}
        </li>
      ))}
    </ul>
  );
}

/** A list of videos and one player; each first play is recorded, as the old page did. */
function Videos({ videos }: { videos: EventVideo[] }) {
  const [current, setCurrent] = useState(videos[0]);
  const played = useRef(new Set<number>());

  return (
    <section className="event-section" aria-labelledby="videos-title">
      <h2 id="videos-title" className="event-section__title">
        Vídeos
      </h2>
      <div className="event-video">
        <p className="event-video__title">{current.title}</p>
        <video
          key={current.id}
          className="event-video__player"
          controls
          playsInline
          preload="metadata"
          onPlay={() => {
            if (played.current.has(current.id)) return;
            played.current.add(current.id);
            void eventsApi.videoPlayed(current.id);
          }}
        >
          <source src={current.url} type={current.mimeType} />
          {current.mimeType === 'video/quicktime' && <source src={current.url} type="video/mp4" />}
        </video>
        <p className="note">Se o vídeo não abrir, experimente outro navegador: alguns telemóveis gravam num formato pouco suportado.</p>
      </div>
      {videos.length > 1 && (
        <ul className="event-video__list">
          {videos.map((v) => (
            <li key={v.id}>
              <button
                type="button"
                className={v.id === current.id ? 'video-pick video-pick--on' : 'video-pick'}
                aria-pressed={v.id === current.id}
                onClick={() => setCurrent(v)}
              >
                <Icon name="play" />
                {v.title}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
