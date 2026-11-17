// The event page's content: description, the member panel, who is going, prizes and videos.

import { eventActivity, CalendarOfferDialog } from './CalendarOffer';
import { useState } from 'react';
import { MyStatus } from './EventsCards';
import { portal } from '../data/content';
import { dateLabel, timeLabel, type EventDetail as Detail } from '../data/eventsApi';
import { Icon } from './icons';
import { WhoIsGoing } from './EventDetailPeople';
import { Videos } from './EventDetailVideos';

export function Body({
  detail,
  onAnswer,
  onManageVideos,
  onManageRepertoire,
  onManageParticipants,
}: {
  detail: Detail;
  onAnswer: () => void;
  onManageVideos?: () => void;
  onManageRepertoire?: () => void;
  onManageParticipants?: () => void;
}) {
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
            {event.membersOnly && <span className="tag tag--private">Só membros</span>}
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

          {member && (member.repertoire.length > 0 || onManageRepertoire) && (
            <section className="event-section" aria-labelledby="repertoire-title">
              <div className="event-section__head">
                <h2 id="repertoire-title" className="event-section__title">
                  Repertório
                </h2>
                {onManageRepertoire && (
                  <button type="button" className="btn btn--ghost btn--sm" onClick={onManageRepertoire}>
                    <Icon name="music" />
                    Gerir repertório
                  </button>
                )}
              </div>
              {member.repertoire.length === 0 && <p className="note">Ainda sem repertório nesta atuação.</p>}
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

          {member && !event.cancelled && <WhoIsGoing participants={member.participants} past={event.past} onManage={onManageParticipants} />}

          {(detail.videos.length > 0 || onManageVideos) && <Videos videos={detail.videos} onManage={onManageVideos} />}

          {!detail.isMember && detail.videos.length === 0 && (
            <p className="note">{event.past ? 'Ainda não há vídeos desta atuação.' : 'Mais novidades nas redes da RTUB.'}</p>
          )}
        </div>

        {detail.isMember && event.member && <MemberPanel detail={detail} onAnswer={onAnswer} />}
      </div>
    </>
  );
}

export function MemberPanel({ detail, onAnswer }: { detail: Detail; onAnswer: () => void }) {
  const { event } = detail;
  const m = event.member!;
  const open = !event.past && !event.cancelled;
  const [calendar, setCalendar] = useState(false);
  const answer = m.myStatus === 'going' ? 'Disseste que vais.' : m.myStatus === 'notGoing' ? 'Disseste que não vais.' : 'Ainda não respondeste.';

  return (
    <aside className="member-panel" aria-labelledby="member-panel-title">
      <h2 id="member-panel-title" className="member-panel__title">
        Área da atuação
      </h2>
      {open ? (
        <>
          <p className="member-panel__answer">{answer}</p>
          <button type="button" className="btn btn--primary" onClick={onAnswer}>
            <Icon name="check" />
            {m.myStatus ? 'Alterar resposta' : 'Responder'}
          </button>
          {m.myStatus === 'going' && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setCalendar(true)}>
              <Icon name="calendar" />
              Adicionar ao calendário
            </button>
          )}
          {calendar && <CalendarOfferDialog activity={eventActivity(event)} headline={null} onClose={() => setCalendar(false)} />}
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
        <li>
          <a href={portal.eventDiscussion(event.id)}>
            <span className="member-panel__icon">
              <Icon name="chatFill" />
            </span>
            Discussão{m.discussionCount > 0 && ` (${m.discussionCount})`}
          </a>
        </li>
        {detail.canTrackContacts && (
          <li>
            {/* Mod and above, or a Leitão (the server refuses anyone else). */}
            <a href={portal.eventContacts(event.id)}>
              <span className="member-panel__icon">
                <Icon name="contactsFill" />
              </span>
              Contactos
            </a>
          </li>
        )}
      </ul>
    </aside>
  );
}
