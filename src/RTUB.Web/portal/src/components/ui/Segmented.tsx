// The segmented-control frame; the caller renders the options (pressed buttons, tabs or radio labels).
import type { ReactNode } from 'react';

export function Segmented({
  label,
  role,
  wrap,
  children,
}: {
  label: string;
  role: 'group' | 'radiogroup' | 'tablist';
  wrap?: boolean;
  children: ReactNode;
}) {
  return (
    <div className={wrap ? 'segmented segmented--wrap' : 'segmented'} role={role} aria-label={label}>
      {children}
    </div>
  );
}
