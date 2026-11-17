// One card as drawn in a board column.
import type { DragEvent, ReactNode } from 'react';
import { Icon } from './icons';
import { PRIORITY, STATUS, type Card } from '../data/logisticsApi';
import { day } from '../data/format';
import { deadline } from '../data/logisticsDates';
import { Menu } from './LogisticsMenu';

const HEX = /^#[0-9a-f]{6}$/i;

const labelColour = (c: string) => (HEX.test(c) ? c : '#6f42c1');

export function CardFace({
  card,
  canManage,
  canMove,
  dragging,
  onOpen,
  onDragStart,
  onDragEnd,
  menu,
}: {
  card: Card;
  canManage: boolean;
  /** Task 067: managers, and a card's owner / collaborators, move it (drag or "Mover…"). */
  canMove: boolean;
  dragging: boolean;
  onOpen: () => void;
  onDragStart: (e: DragEvent<HTMLElement>) => void;
  onDragEnd: () => void;
  menu: (close: () => void) => ReactNode;
}) {
  const s = card.status.toLowerCase();
  const progress = card.checklistTotal ? Math.round((card.checklistDone / card.checklistTotal) * 100) : 0;
  const due = deadline(card.dueDate, card.status);
  return (
    <article
      className={`lx-card lx-card--${s} lx-card--open${dragging ? ' lx-card--dragging' : ''}${card.mine ? ' lx-card--mine' : ''}`}
      data-card={card.id}
      draggable={canMove}
      onDragStart={canMove ? onDragStart : undefined}
      onDragEnd={canMove ? onDragEnd : undefined}
      // The whole card opens the details (a native drag cannot start on a button); the title button is the keyboard path.
      onClick={onOpen}
    >
      <div className="lx-card__top">
        <span className={`lx-status lx-status--${s}`}>{STATUS[card.status].label}</span>
        {(card.startDate || card.dueDate) && (
          <span className="lx-card__dates">
            <Icon name="calendar" />
            {day(card.startDate)}
            {card.startDate && card.dueDate && ' – '}
            {day(card.dueDate)}
          </span>
        )}
        {card.priority !== 'Normal' && <span className={`lx-prio lx-prio--${card.priority.toLowerCase()}`}>{PRIORITY[card.priority]}</span>}
        {due && <span className={`lx-due lx-due--${due.tone}`}>{due.text}</span>}
        {card.mine && <span className="lx-chip lx-chip--mine">Atribuído a si</span>}
        {(canManage || canMove) && <Menu label={`Ações do cartão ${card.title}`}>{menu}</Menu>}
      </div>
      <h3 className="lx-card__title">
        <button
          type="button"
          className="lx-card__button"
          onClick={(e) => {
            e.stopPropagation();
            onOpen();
          }}
        >
          {card.title}
        </button>
      </h3>
      {card.description && <p className="lx-card__desc">{card.description}</p>}
      {card.labels.length > 0 && (
        <ul className="lx-labels">
          {card.labels.map((l, i) => (
            <li key={`${l.text}-${i}`} className="lx-label">
              <span className="lx-label__dot" style={{ background: labelColour(l.color) }} aria-hidden="true" />
              {l.text}
            </li>
          ))}
        </ul>
      )}
      {card.checklistTotal > 0 && (
        <div className="lx-progress" title="Checklist">
          <span className="lx-progress__bar">
            <span style={{ width: `${progress}%` }} />
          </span>
          <small>
            {card.checklistDone}/{card.checklistTotal}
          </small>
        </div>
      )}
      {/* Always shown: the owner, or "Sem responsável" (task 066). */}
      <div className="lx-card__meta">
        {card.eventName && (
          <span>
            <Icon name="calendar" />
            {card.eventName}
          </span>
        )}
        {card.assignedTo ? (
          <span title={card.assignedTo.fullName ?? 'Responsável'}>
            <Icon name="person" />
            {card.assignedTo.displayName}
          </span>
        ) : (
          <span className="lx-card__unowned">Sem responsável</span>
        )}
        {card.assignments > 0 && (
          <span title="Colaboradores">
            <Icon name="people" />
            {card.assignments}
          </span>
        )}
        {card.comments > 0 && (
          <span title="Comentários">
            <Icon name="chat" />
            {card.comments}
          </span>
        )}
        {card.attachments > 0 && (
          <span title="Anexos">
            <Icon name="file" />
            {card.attachments}
          </span>
        )}
        {card.links > 0 && (
          <span title="Cartões ligados">
            <Icon name="link" />
            {card.links}
          </span>
        )}
      </div>
    </article>
  );
}
