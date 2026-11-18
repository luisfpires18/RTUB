// One titled group of member cards on the /members page.
import { Icon } from './icons';
import { MemberBadge } from './MemberBadge';
import { type MemberCard } from '../data/membersApi';
import { Avatar } from './ui/Avatar';

export function Group({ title, people, limit, onMore, onOpen }: { title: string; people: MemberCard[]; limit: number; onMore: () => void; onOpen: (id: string) => void }) {
  return (
    <section className="events-block" aria-label={title}>
      <h2 className="events-block__title">
        {title} · {people.length}
      </h2>
      <ul className="who members-grid">
        {people.slice(0, limit).map((p) => (
          <li key={p.id}>
            <button type="button" className={p.expelled ? 'who__person member-card member-card--expelled' : 'who__person member-card'} onClick={() => onOpen(p.id)}>
              <span className={p.online ? 'member-card__online is-on' : 'member-card__online'}>{p.online ? 'Online' : 'Offline'}</span>
              <Avatar className="who__avatar" src={p.avatarUrl} size={72} />
              <span className="who__name">{p.displayName}</span>
              {p.fullName && <span className="who__meta">{p.fullName}</span>}
              <span className="member-badges">
                {p.badges.map((b) => (
                  <MemberBadge key={b.kind} kind={b.kind}>{b.label}</MemberBadge>
                ))}
                {p.position && <MemberBadge kind="position">{p.position}</MemberBadge>}
                {p.noRoles && <MemberBadge>Sem cargos</MemberBadge>}
              </span>
              {p.instrument && <span className="who__meta">{p.instrument}</span>}
              {p.inactive && (
                <span className="member-card__inactive">
                  <Icon name="clock" />
                  Não tem participado
                </span>
              )}
            </button>
          </li>
        ))}
      </ul>
      {people.length > limit && (
        <button type="button" className="btn btn--ghost btn--sm members-more" onClick={onMore}>
          Mostrar mais ({people.length - limit})
        </button>
      )}
    </section>
  );
}
