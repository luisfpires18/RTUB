import { type ReactNode } from 'react';
import { Dialog } from './Dialog';
import { Icon, type IconName } from './icons';
import { IconAction } from './ui/IconAction';

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
  return <IconAction icon={icon} label={label} title={label} onClick={onClick} danger={tone === 'danger'} disabled={disabled} />;
}

export const plays = (n: number) => `${n} ${n === 1 ? 'reprodução' : 'reproduções'}`;
