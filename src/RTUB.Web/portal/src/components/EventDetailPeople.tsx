// "Quem vai / Quem foi" on the event page: who answered, as avatar-led tiles.

import { EmptyNote } from './ui/EmptyNote';
import type { EventParticipant } from '../data/eventsApi';
import { Icon } from './icons';
import { MemberBadge, badgeKind } from './MemberBadge';
import { Avatar } from './ui/Avatar';

/** "Quem vai" before and during an event, "Quem foi" once it is over. */
const whoLabel = (past: boolean) => (past ? 'Quem foi' : 'Quem vai');

/** Who answered, on this page: members going, Leitões going, and (folded) who is not going. */
export function WhoIsGoing({
  participants,
  past,
  onManage,
}: {
  participants: { going: EventParticipant[]; leitoes: EventParticipant[]; notGoing: EventParticipant[] };
  past: boolean;
  onManage?: () => void;
}) {
  const { going, leitoes, notGoing } = participants;
  const total = going.length + leitoes.length;

  return (
    <section className="event-section" aria-labelledby="who-title">
      <div className="event-section__head">
        <h2 id="who-title" className="event-section__title">
          {whoLabel(past)} <span className="event-section__count">{total}</span>
        </h2>
        {onManage && (
          <button type="button" className="btn btn--ghost btn--sm" onClick={onManage}>
            <Icon name="person" />
            Gerir inscrições
          </button>
        )}
      </div>
      {total === 0 && notGoing.length === 0 ? (
        <EmptyNote>Ainda ninguém respondeu.</EmptyNote>
      ) : (
        <>
          {going.length > 0 && <People people={going} />}
          {leitoes.length > 0 && (
            <>
              <h3 className="who__group">Leitões · {leitoes.length}</h3>
              <People people={leitoes} />
            </>
          )}
          {total === 0 && <EmptyNote>{past ? 'Ninguém confirmou que foi.' : 'Por agora ninguém confirmou.'}</EmptyNote>}
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
        <li key={p.key ?? i} className="who__person" title={p.fullName ?? undefined}>
          <Avatar className="who__avatar" src={p.avatarUrl} size={72} />
          <p className="who__name">{p.name}</p>
          {p.badge && <MemberBadge kind={badgeKind(p.badge)}>{p.badge}</MemberBadge>}
          {p.instrument && <p className="who__meta">{p.instrument}</p>}
          {p.notes && <p className="who__note">“{p.notes}”</p>}
        </li>
      ))}
    </ul>
  );
}
