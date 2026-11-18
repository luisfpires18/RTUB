// A person on one line: round photo, name, and a small note under it (role, date).
import type { ReactNode } from 'react';
import { Avatar } from './Avatar';

/** `badge` sits between the name and the note; without `note` and `badge` the name stands alone. */
export function PersonLine({
  name,
  avatar,
  size = 32,
  note,
  badge,
}: {
  name: string;
  avatar: string | null;
  size?: number;
  note?: ReactNode;
  badge?: ReactNode;
}) {
  return (
    <span className="qs-person">
      <Avatar className="who__avatar" src={avatar} size={size} />
      {note || badge ? (
        <span>
          <strong>{name}</strong>
          {badge}
          {note && <small className="note">{note}</small>}
        </span>
      ) : (
        <strong>{name}</strong>
      )}
    </span>
  );
}
