// A small icon-only button (edit, delete, move) with its name for screen readers.
import { Icon, type IconName } from '../icons';

/** `label` is the screen-reader text; `title` the hover tooltip (usually shorter). `danger` is the red variant. */
export function IconAction({
  icon,
  label,
  title,
  onClick,
  danger,
  disabled,
}: {
  icon: IconName;
  label: string;
  title?: string;
  onClick: () => void;
  danger?: boolean;
  disabled?: boolean;
}) {
  return (
    <button type="button" className={danger ? 'icon-btn icon-btn--sm icon-btn--danger' : 'icon-btn icon-btn--sm'} onClick={onClick} title={title} disabled={disabled}>
      <Icon name={icon} />
      <span className="sr-only">{label}</span>
    </button>
  );
}
