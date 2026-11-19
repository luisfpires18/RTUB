// The "Detalhes da Reunião" dialog of /meetings.
import { Dialog } from './Dialog';
import type { MeetingCard } from '../data/meetingsTypes';
import { numericDateTime } from '../data/meetingsDates';
import { typeClass, Detail } from './MeetingUi';

export function DetailsDialog({ card, onClose }: { card: MeetingCard; onClose: () => void }) {
  return (
    <Dialog
      title="Detalhes da Reunião"
      size="lg"
      onClose={onClose}
      footer={
        <button type="button" className="btn btn--ghost" onClick={onClose}>
          Voltar Atrás
        </button>
      }
    >
      <div className="mtg-detail">
        <span className={typeClass(card.type)}>{card.typeLabel}</span>
        <h3 className="mtg-detail__title">{card.title}</h3>
        <dl className="mtg-detail__list">
          <Detail icon="calendar" label="Data e Hora">
            {numericDateTime(card.date)}
          </Detail>
          {card.location && (
            <Detail icon="geo" label="Localização">
              {card.location}
            </Detail>
          )}
          {card.organizerName && (
            <Detail icon="person" label="Organizador">
              {card.organizerName}
              {card.organizerPosition && <span className="note"> ({card.organizerPosition})</span>}
            </Detail>
          )}
          {card.type === 'ConselhoVeteranos' && card.tunoRepresentative && (
            <Detail icon="personBadge" label="Representante de Tunos">
              {card.tunoRepresentative}
            </Detail>
          )}
          <Detail icon="file" label="Declaração">
            <span className="mtg-pre">{card.statement}</span>
          </Detail>
        </dl>
      </div>
    </Dialog>
  );
}
