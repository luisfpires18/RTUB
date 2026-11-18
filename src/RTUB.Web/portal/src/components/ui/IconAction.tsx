// An icon-only button (edit, delete, move, close) with its name for screen readers.
import { Icon, type IconName } from '../icons';

/**
 * `label` is the screen-reader text; `title` the hover tooltip (usually shorter). `danger` is the red variant.
 * The button is the small one (`icon-btn--sm`) unless `large`; `className` adds the place's own class after those.
 */
export function IconAction({
  icon,
  label,
  title,
  onClick,
  danger,
  large,
  className,
  disabled,
}: {
  icon: IconName;
  label: string;
  title?: string;
  onClick: () => void;
  danger?: boolean;
  large?: boolean;
  className?: string;
  disabled?: boolean;
}) {
  const classes = ['icon-btn', !large && 'icon-btn--sm', danger && 'icon-btn--danger', className].filter(Boolean).join(' ');
  return (
    <button type="button" className={classes} onClick={onClick} title={title} disabled={disabled}>
      <Icon name={icon} />
      <span className="sr-only">{label}</span>
    </button>
  );
}
