// A picked member as a chip (photo + name), with a remove button when `onRemove` is given.
import { Icon } from '../icons';
import { DEFAULT_AVATAR } from '../../data/membersApi';

export function PersonChip({
  name,
  avatar,
  onRemove,
  removeLabel = 'Escolher outro membro',
}: {
  name: string;
  avatar: string | null;
  onRemove?: () => void;
  removeLabel?: string;
}) {
  return (
    <span className="chip chip--removable">
      <img src={avatar ?? DEFAULT_AVATAR} alt="" width="24" height="24" />
      {name}
      {onRemove && (
        <button type="button" className="chip__remove" onClick={onRemove}>
          <Icon name="close" />
          <span className="sr-only">{removeLabel}</span>
        </button>
      )}
    </span>
  );
}
