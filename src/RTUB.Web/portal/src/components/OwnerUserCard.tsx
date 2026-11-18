// Owner users member card.
import { Icon } from './icons';
import { dateTime, type OwnerUserCard } from '../data/ownerApi';
import { MemberBadge } from './MemberBadge';
import { Avatar } from './ui/Avatar';

export function UserCard({ user, onDetails, onEdit }: { user: OwnerUserCard; onDetails: () => void; onEdit: () => void }) {
  return (
    <article className="owner-user">
      <Avatar className="owner-user__avatar" src={user.avatarUrl} size={64} />
      <div className="owner-user__body">
        <p className={user.online ? 'owner-user__status is-online' : 'owner-user__status'}>
          <span aria-hidden="true" />
          {user.online ? 'Online' : 'Offline'}
        </p>
        <h2 className="owner-user__name">{user.nickname || user.userName}</h2>
        <p className="note">{user.fullName}</p>
        <p className="member-badges">
          <MemberBadge kind="position">{user.role}</MemberBadge>
          {user.locked && <MemberBadge kind="danger">Bloqueado</MemberBadge>}
        </p>
        <p className="owner-user__meta">
          <code>{user.userName}</code>
          <span>{user.email}</span>
          <span>Último login: {dateTime(user.lastLoginAt)}</span>
        </p>
      </div>
      <div className="owner-user__actions">
        <button type="button" className="btn btn--ghost btn--sm" onClick={onDetails}>
          <Icon name="eye" />
          Detalhes<span className="sr-only"> de {user.nickname}</span>
        </button>
        <button type="button" className="btn btn--ghost btn--sm" onClick={onEdit}>
          <Icon name="pencil" />
          Função<span className="sr-only"> de {user.nickname}</span>
        </button>
      </div>
    </article>
  );
}
