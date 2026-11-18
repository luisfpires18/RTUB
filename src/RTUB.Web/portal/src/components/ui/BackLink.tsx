// The "← back" link above a page title.
import type { ReactNode } from 'react';
import { Icon, type IconName } from '../icons';

export function BackLink({ href, icon = 'arrow', children }: { href: string; icon?: IconName; children: ReactNode }) {
  return (
    <a className="back-link" href={href}>
      <Icon name={icon} />
      {children}
    </a>
  );
}
