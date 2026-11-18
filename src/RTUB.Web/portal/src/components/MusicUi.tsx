import { type ReactNode } from 'react';
import { Dialog } from './Dialog';
import { Icon, type IconName } from './icons';

export { Dialog };

export function Badge({ icon, children }: { icon: IconName; children: ReactNode }) {
  return (
    <span className="badge">
      <Icon name={icon} />
      {children}
    </span>
  );
}

export function IconButton({
  icon,
  label,
  onClick,
  tone,
  disabled,
}: {
  icon: IconName;
  label: string;
  onClick: () => void;
  tone?: 'danger';
  disabled?: boolean;
}) {
  return (
    <button type="button" className={`icon-btn icon-btn--sm${tone ? ` icon-btn--${tone}` : ''}`} onClick={onClick} disabled={disabled} title={label}>
      <Icon name={icon} />
      <span className="sr-only">{label}</span>
    </button>
  );
}

export const plays = (n: number) => `${n} ${n === 1 ? 'reprodução' : 'reproduções'}`;
