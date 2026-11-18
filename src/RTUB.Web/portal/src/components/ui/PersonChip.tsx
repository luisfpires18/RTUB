// A member as a chip (photo + name): removable by default, or `readOnly` with a small note.
import { Icon } from '../icons';
import { DEFAULT_AVATAR } from '../../data/membersApi';

/**
 * Removable (default): `chip chip--removable`, with a remove button only when `onRemove` is given.
 * `item`: an `<li>` instead of a `<span>` (chips inside a `<ul>`).
 * `readOnly`: `chip lx-person` with an optional small `note` after the name; no remove button.
 */
export function PersonChip({
  name,
  avatar,
  onRemove,
  removeLabel = 'Escolher outro membro',
  item,
  readOnly,
  note,
}: {
  name: string;
  avatar: string | null;
  onRemove?: () => void;
  removeLabel?: string;
  item?: boolean;
  readOnly?: boolean;
  note?: string;
}) {
  const Tag = item ? 'li' : 'span';
  return (
    <Tag className={readOnly ? 'chip lx-person' : 'chip chip--removable'}>
      <img src={avatar ?? DEFAULT_AVATAR} alt="" width="24" height="24" />
      {name}
      {readOnly && note && <small className="lx-view__lead">{note}</small>}
      {!readOnly && onRemove && (
        <button type="button" className="chip__remove" onClick={onRemove}>
          <Icon name="close" />
          <span className="sr-only">{removeLabel}</span>
        </button>
      )}
    </Tag>
  );
}
