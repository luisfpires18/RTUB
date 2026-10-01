import { useEffect, useRef, useState } from 'react';
import { Loading } from './App';
import { legacy, portal } from './content';
import { MyStatus } from './Events';
import { dateLabel, eventsApi, timeLabel, type EventDetail as Detail, type EventVideo } from './eventsApi';
import { Icon } from './icons';

/**
 * /events/{id} - one event. Visitors get the public facts, prizes and videos; signed-in members
 * also get the description, their own answer (answered on /events/{id}/attendance), the counts,
 * the repertoire, and links to the members' participants and discussion pages.
 */
export default function EventDetail({ eventId }: { eventId: number }) {
  const [detail, setDetail] = useState<Detail | 'missing' | null>();

  const load = () => {
    setDetail(undefined);
    eventsApi.event(eventId).then((o) => setDetail(o.kind === 'ok' ? o.data : o.kind === 'notfound' ? 'missing' : null));
  };
  useEffect(load, [eventId]);

  useEffect(() => {
    document.title = detail && detail !== 'missing' ? `${detail.event.name} · Atuações · RTUB` : 'Atuações · RTUB';
  }, [detail]);

  return (
    <section className="page wrap event-page" aria-labelledby="event-title">
      <a className="back-link" href={portal.events}>
        <Icon name="arrow" />
        Agenda
      </a>
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
          <button type="button" className="btn btn--ghost btn--sm" onClick={load}>
            Tentar novamente
          </button>
        </div>
      ) : (
        <Body detail={detail} />
      )}
    </section>
  );
}

function Body({ detail }: { detail: Detail }) {
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

      <div className="event-layout">
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

          {event.trophies.length > 0 && (
            <section className="event-section" aria-labelledby="trophies-title">
              <h2 id="trophies-title" className="event-section__title">
                Prémios conquistados
              </h2>
              <ul className="trophies">
                {event.trophies.map((t, i) => (
                  <li key={i}>
                    <Icon name="trophy" />
                    {t}
                  </li>
                ))}
              </ul>
            </section>
          )}

          {detail.videos.length > 0 && <Videos videos={detail.videos} />}

          {!detail.isMember && event.trophies.length === 0 && detail.videos.length === 0 && (
            <p className="note">{event.past ? 'Sem vídeos nem prémios registados.' : 'Mais detalhes nas redes da RTUB.'}</p>
          )}
        </div>

        {detail.isMember && event.member && <MemberPanel detail={detail} />}
      </div>
    </>
  );
}

function MemberPanel({ detail }: { detail: Detail }) {
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
          <a className="btn btn--primary" href={portal.eventAttendance(event.id)}>
            <Icon name="check" />
            {m.myStatus ? 'Alterar a resposta' : 'Responder'}
          </a>
        </>
      ) : event.past && m.myStatus === 'going' && !event.cancelled ? (
        <a className="btn btn--ghost btn--sm" href={portal.eventAttendance(event.id)}>
          A minha presença
        </a>
      ) : null}
      <dl className="member-panel__counts">
        <div>
          <dt>Vão</dt>
          <dd>{m.goingCount}</dd>
        </div>
        <div>
          <dt>Não vão</dt>
          <dd>{detail.member?.notGoingCount ?? 0}</dd>
        </div>
        <div>
          <dt>Músicas</dt>
          <dd>{m.repertoireCount}</dd>
        </div>
      </dl>
      <ul className="member-panel__links">
        <li>
          <a href={legacy.eventEnrollments(event.id)}>
            <Icon name="person" />
            Quem vai
          </a>
        </li>
        <li>
          <a href={legacy.eventDiscussion(event.id)}>
            <Icon name="envelope" />
            Discussão{m.discussionCount > 0 && ` (${m.discussionCount})`}
          </a>
        </li>
        <li>
          <a href={legacy.memberEvents}>
            <Icon name={detail.canManage ? 'pencil' : 'upload'} />
            {detail.canManage ? 'Gerir atuações' : 'Carregar vídeos'}
          </a>
        </li>
      </ul>
    </aside>
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
