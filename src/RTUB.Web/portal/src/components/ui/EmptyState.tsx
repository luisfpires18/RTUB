// The centred "nothing here / failed / refused" block: icon, title, text (children) and actions.
import type { ReactNode } from 'react';
import { Icon, type IconName } from '../icons';

/** `className` is added after "state"; `heading` makes the title an h1 (a whole-page state). */
export function EmptyState({
  icon,
  title,
  titleId,
  heading,
  className,
  role,
  actions,
  children,
}: {
  icon?: IconName;
  title: ReactNode;
  titleId?: string;
  heading?: boolean;
  className?: string;
  role?: 'status' | 'alert';
  actions?: ReactNode;
  children?: ReactNode;
}) {
  const Title = heading ? 'h1' : 'p';
  return (
    <div className={className ? `state ${className}` : 'state'} role={role}>
      {icon && <Icon name={icon} className="state__icon" />}
      <Title id={titleId} className="state__title">
        {title}
      </Title>
      {children}
      {actions && <div className="state__actions">{actions}</div>}
    </div>
  );
}
