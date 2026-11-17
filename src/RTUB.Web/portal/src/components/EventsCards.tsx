// Agenda cards: the caller's answer pill, manage buttons, the upcoming card and the archive row.

import { portal } from '../data/content';
import { dayOf, monthShort, yearOf, dateLabel, timeLabel, type EventSummary } from '../data/eventsApi';
import { Icon } from './icons';

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
export type Manage =
  | { onEdit: () => void; onDelete?: () => void; onNotice?: () => void; onCancel?: () => void }
  | undefined;

/** Edit (Mod and above), notice, cancel / reactivate and delete (Admin/Owner), over the card's stretched link. */
function ManageButtons({ event, manage }: { event: EventSummary; manage: Manage }) {
  if (!manage) return null;
  return (
    <span className="manage">
      <button type="button" className="icon-btn icon-btn--sm" onClick={manage.onEdit} title="Editar">
        <Icon name="pencil" />
        <span className="sr-only">Editar {event.name}</span>
      </button>
      {manage.onNotice && (
        <button type="button" className="icon-btn icon-btn--sm" onClick={manage.onNotice} title="Enviar aviso">
          <Icon name="bell" />
          <span className="sr-only">Enviar aviso sobre {event.name}</span>
        </button>
      )}
      {manage.onCancel && (
        <button type="button" className="icon-btn icon-btn--sm" onClick={manage.onCancel} title={event.cancelled ? 'Reativar' : 'Cancelar atuação'}>
          <Icon name={event.cancelled ? 'restore' : 'ban'} />
          <span className="sr-only">
            {event.cancelled ? 'Reativar' : 'Cancelar'} {event.name}
          </span>
        </button>
      )}
      {manage.onDelete && (
        <button type="button" className="icon-btn icon-btn--sm icon-btn--danger" onClick={manage.onDelete} title="Apagar">
          <Icon name="trash" />
          <span className="sr-only">Apagar {event.name}</span>
        </button>
      )}
    </span>
  );
}

export function AgendaCard({ event, onAnswer, manage }: { event: EventSummary; onAnswer: () => void; manage: Manage }) {
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
          {event.membersOnly && <span className="tag tag--private">Só membros</span>}
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
export function ArchiveRow({ event, manage }: { event: EventSummary; manage: Manage }) {
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
          {event.membersOnly && ' · só membros'}
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
