// One meeting card on /meetings (status, details and action buttons).
import { Icon } from './icons';
import { typeClass } from './MeetingUi';
import type { MeetingCard } from '../data/meetingsTypes';
import { dayBadge, shortDate, timeOf, weekdayName } from '../data/meetingsDates';
import { type Open } from './MeetingDialogHost';
import { IconAction } from './ui/IconAction';

export function MeetingCardView({ card, onOpen }: { card: MeetingCard; onOpen: (o: Open) => void }) {
  const status = card.cancelled ? 'Cancelada' : card.past ? 'Realizada' : 'Agendada';
  const statusClass = card.cancelled ? 'mtg-status mtg-status--cancelled' : card.past ? 'mtg-status mtg-status--past' : 'mtg-status mtg-status--upcoming';
  const mine = card.myParticipation;
  const soon = card.cancelled ? null : dayBadge(card.date);

  return (
    <article className={card.cancelled ? 'mtg-card is-cancelled' : 'mtg-card'}>
      <div className="mtg-card__top">
        <span className="mtg-card__badges">
          <span className={statusClass}>{status}</span>
          {mine && (
            <span className={mine.willAttend ? 'pill pill--yes' : 'pill pill--no'}>
              <Icon name={mine.willAttend ? 'checkCircle' : 'xCircle'} />
              {mine.willAttend ? (card.completed ? 'FUI' : 'VOU') : card.completed ? 'NÃO FUI' : 'NÃO VOU'}
            </span>
          )}
          {soon && <span className="mtg-soon">{soon}</span>}
        </span>
        {card.can.manage && (
          <span className="mtg-card__tools">
            <IconAction icon="pencil" label={`Editar reunião ${card.title}`} title="Editar" onClick={() => onOpen({ kind: 'form', card })} />
            <IconAction icon="trash" label={`Eliminar reunião ${card.title}`} title="Eliminar" onClick={() => onOpen({ kind: 'delete', card })} danger />
          </span>
        )}
      </div>

      <div className="mtg-card__head">
        <h3 className="mtg-card__title">{card.title}</h3>
        <span className={typeClass(card.type)}>{card.typeLabel}</span>
      </div>

      <dl className="mtg-facts">
        <div>
          <dt>
            <Icon name="calendar" />
            <span className="sr-only">Data</span>
          </dt>
          <dd>
            {shortDate(card.date)} ({weekdayName(card.date)})
          </dd>
        </div>
        <div>
          <dt>
            <Icon name="clock" />
            <span className="sr-only">Hora</span>
          </dt>
          <dd>{timeOf(card.date)}</dd>
        </div>
        {card.location && (
          <div>
            <dt>
              <Icon name="geo" />
              <span className="sr-only">Local</span>
            </dt>
            <dd className="mtg-break">{card.location}</dd>
          </div>
        )}
        {card.organizerPosition && (
          <div>
            <dt>
              <Icon name="personBadge" />
              <span className="sr-only">Organizador</span>
            </dt>
            <dd>
              Organizador: {card.organizerName}, {card.organizerPosition}
            </dd>
          </div>
        )}
        {card.tunoRepresentative && (
          <div>
            <dt className="mtg-facts__gold">
              <Icon name="personBadge" />
              <span className="sr-only">Representante</span>
            </dt>
            <dd>Representante dos Tunos: {card.tunoRepresentative}</dd>
          </div>
        )}
      </dl>

      {(card.can.participants || card.can.respond || card.can.removeOwn) && (
        <div className="mtg-answer">
          {card.can.participants && (
            <button type="button" className="mtg-count" onClick={() => onOpen({ kind: 'participants', card })} title="Participantes">
              <Icon name="people" />
              <span>{card.goingCount}</span>
              <span className="sr-only">{card.goingCount === 1 ? 'participante — ver participantes' : 'participantes — ver participantes'}</span>
            </button>
          )}
          {card.can.respond && (
            <span className="mtg-answer__choices">
              <button
                type="button"
                className={mine?.willAttend === true ? 'mtg-choice mtg-choice--yes is-on' : 'mtg-choice mtg-choice--yes'}
                aria-pressed={mine?.willAttend === true}
                onClick={() => onOpen({ kind: 'respond', card, willAttend: true })}
                title={mine?.willAttend === true ? 'Editar participação' : 'Vou participar'}
              >
                <Icon name="checkCircle" />
                Vou
              </button>
              <button
                type="button"
                className={mine?.willAttend === false ? 'mtg-choice mtg-choice--no is-on' : 'mtg-choice mtg-choice--no'}
                aria-pressed={mine?.willAttend === false}
                onClick={() => onOpen({ kind: 'respond', card, willAttend: false })}
                title={mine?.willAttend === false ? 'Editar impossibilidade' : 'Não vou participar'}
              >
                <Icon name="xCircle" />
                Não vou
              </button>
            </span>
          )}
          {card.can.removeOwn && mine && (
            <button type="button" className="btn btn--ghost btn--sm mtg-remove-own" onClick={() => onOpen({ kind: 'removeOwn', card })}>
              <Icon name="xCircle" />
              Remover participação
            </button>
          )}
        </div>
      )}

      <div className="mtg-card__actions">
        <button type="button" className="btn btn--primary btn--sm" onClick={() => onOpen({ kind: 'details', card })}>
          <Icon name="eye" />
          Ver Detalhes
        </button>
        {card.can.writeAta && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => onOpen({ kind: 'ata', card })} title="Criar/Editar Ata">
            <Icon name="file" />
            Ata
          </button>
        )}
        {card.can.viewAta && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => onOpen({ kind: 'viewAta', card })}>
            <Icon name="eye" />
            Ver Ata
            {card.ataStatus === 'draft' && <span className="mtg-tag">Rascunho</span>}
          </button>
        )}
        {card.can.notify && (
          <>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => onOpen({ kind: 'email', card })} title="Enviar notificação por email">
              <Icon name="envelope" />
              Email
            </button>
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => onOpen({ kind: 'push', card })} title="Enviar notificação push">
              <Icon name="bell" />
              Push
            </button>
            <button type="button" className="btn btn--danger btn--sm" onClick={() => onOpen({ kind: 'cancel', card })} title="Cancelar reunião">
              <Icon name="xCircle" />
              Cancelar
            </button>
          </>
        )}
        {card.can.uncancel && (
          <button type="button" className="btn btn--gold btn--sm" onClick={() => onOpen({ kind: 'uncancel', card })} title="Reverter cancelamento">
            <Icon name="restore" />
            Reverter
          </button>
        )}
      </div>
    </article>
  );
}
