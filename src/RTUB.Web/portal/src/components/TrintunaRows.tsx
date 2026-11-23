// The /trintuna list rows: a Próximos card and a Passados row, with the manage buttons (split from pages/Trintuna.tsx).
import type { Gathering } from '../data/trintunaApi';
import { dayOf, monthShort, yearOf, dateLabel } from '../data/eventsDates';
import { Icon } from './icons';
import { IconAction } from './ui/IconAction';

export type Tools = { onEdit?: () => void; onDelete?: () => void };

/** Edit (Owner, or the gathering's organizer) and delete (Owner), over the stretched link. */
export function ManageButtons({ g, tools }: { g: Gathering; tools: Tools }) {
  if (!tools.onEdit && !tools.onDelete) return null;
  return (
    <span className="manage">
      {tools.onEdit && <IconAction icon="pencil" title="Editar" label={`Editar ${g.title}`} onClick={tools.onEdit} />}
      {tools.onDelete && <IconAction danger icon="trash" title="Apagar" label={`Apagar ${g.title}`} onClick={tools.onDelete} />}
    </span>
  );
}

/** A Próximos card: the agenda-card markup of /events; the whole card opens the gathering. */
export function UpcomingCard({ g, onOpen, tools }: { g: Gathering; onOpen: () => void; tools: Tools }) {
  return (
    <article className="agenda-card">
      {g.date ? (
        <time className="agenda-card__date" dateTime={g.date}>
          <span className="agenda-card__day">{dayOf(g.date)}</span>
          <span className="agenda-card__month">{monthShort(g.date)}</span>
          <span className="agenda-card__year">{yearOf(g.date)}</span>
        </time>
      ) : (
        <span className="agenda-card__date">
          <span className="agenda-card__month">Data por decidir</span>
        </span>
      )}
      <div className="agenda-card__body">
        <div className="agenda-card__tags">
          <span className="tag">{g.title}</span>
          {!g.date && !g.poll.closed && g.poll.options.length > 0 && <span className="tag tag--private">Votação aberta</span>}
          <ManageButtons g={g} tools={tools} />
        </div>
        <h3 className="agenda-card__name">
          <a className="agenda-card__link" href={`#almoco-${g.id}`} onClick={(e) => (e.preventDefault(), onOpen())}>
            {g.place}
          </a>
        </h3>
        <p className="agenda-card__meta">
          <Icon name="clock" />
          <span>{g.date ? dateLabel({ date: g.date, endDate: null }) : 'Data por decidir'}</span>
        </p>
        {g.organizer && (
          <p className="agenda-card__meta">
            <Icon name="person" />
            <span>Organiza: {g.organizer.displayName}</span>
          </p>
        )}
        <div className="agenda-card__foot">
          <span className="agenda-card__count">{g.goingCount === 1 ? '1 vai' : `${g.goingCount} vão`}</span>
          <button
            type="button"
            className={`quick-reply quick-reply--${g.myStatus ?? 'none'}`}
            onClick={onOpen}
            aria-label={`${g.myStatus ? 'Alterar resposta' : 'Responder'}: ${g.title}`}
          >
            {g.myStatus === 'going' ? (
              <>
                <Icon name="check" />
                Vais
              </>
            ) : g.myStatus === 'notGoing' ? (
              <>
                <Icon name="close" />
                Não vais
              </>
            ) : (
              'Responder'
            )}
          </button>
        </div>
      </div>
      <Icon name="arrow" className="agenda-card__go" />
    </article>
  );
}

/** A Passados row: the archive-row markup of /events. */
export function PastRow({ g, onOpen, tools }: { g: Gathering; onOpen: () => void; tools: Tools }) {
  return (
    <div className="archive-row">
      {g.date ? (
        <time className="archive-row__date" dateTime={g.date}>
          <span>{dayOf(g.date)}</span> {monthShort(g.date)}
        </time>
      ) : (
        <span className="archive-row__date">
          <span>{g.number}º</span>
        </span>
      )}
      <span className="archive-row__main">
        <a className="archive-row__name" href={`#almoco-${g.id}`} onClick={(e) => (e.preventDefault(), onOpen())}>
          {g.title} · {g.place}
        </a>
        <span className="archive-row__meta">
          {[g.description, g.date && yearOf(g.date), g.organizer && `Organizou: ${g.organizer.displayName}`, g.goingCount > 0 && (g.goingCount === 1 ? '1 foi' : `${g.goingCount} foram`)]
            .filter(Boolean)
            .join(' · ')}
        </span>
      </span>
      <span className="archive-row__badges">
        <ManageButtons g={g} tools={tools} />
      </span>
    </div>
  );
}
