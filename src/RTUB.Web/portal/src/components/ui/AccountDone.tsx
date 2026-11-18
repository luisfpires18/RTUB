// The result card of the account pages (reset link sent, e-mail confirmed, nothing shared): icon, message, optional link.
import type { ReactNode, Ref } from 'react';
import { Icon, type IconName } from '../icons';

/** `focusRef` makes the card focusable (tabIndex -1) so the page can move focus to it; `error` is the red variant. */
export function AccountDone({
  icon,
  message,
  error,
  role = 'status',
  focusRef,
  children,
}: {
  icon: IconName;
  message: string;
  error?: boolean;
  role?: 'status' | 'alert';
  focusRef?: Ref<HTMLDivElement>;
  children?: ReactNode;
}) {
  return (
    <div ref={focusRef} tabIndex={focusRef ? -1 : undefined} className={error ? 'account-done account-done--error' : 'account-done'} role={role}>
      <Icon name={icon} />
      <p>{message}</p>
      {children}
    </div>
  );
}
