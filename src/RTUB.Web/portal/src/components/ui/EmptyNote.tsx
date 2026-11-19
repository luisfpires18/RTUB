// "Nothing here yet" lines: a light note (EmptyNote) or a block with an icon (EmptyBlock).
import type { ReactNode } from 'react';
import { Icon, type IconName } from '../icons';

/** `<p className="note">`. `className` replaces "note" when a page has its own look (e.g. "notice naipe-empty"). */
export function EmptyNote({ className = 'note', children }: { className?: string; children: ReactNode }) {
  return <p className={className}>{children}</p>;
}

/**
 * A block with its own class ("upcoming-empty", "talk__empty", "lx-empty"), an optional icon first, then the
 * caller's `<p>`s as children. `role` as the caller had it.
 */
export function EmptyBlock({ className, icon, role, children }: { className: string; icon?: IconName; role?: string; children: ReactNode }) {
  return (
    <div className={className} role={role}>
      {icon && <Icon name={icon} />}
      {children}
    </div>
  );
}
