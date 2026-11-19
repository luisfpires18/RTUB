import type { ReactNode } from 'react';
import { loginTo } from '../data/content';
import { Icon } from './icons';

// The short message a page shows where its content would be. Use these instead of writing the markup again.

/** Just the message. `id` goes on the `<p>` (a heading target); `alert`: announced as an alert, not a status. */
export function Notice({ id, alert, children }: { id?: string; alert?: boolean; children: ReactNode }) {
  return (
    <div className="notice" role={alert ? 'alert' : 'status'}>
      <p id={id}>{children}</p>
    </div>
  );
}

/** The page is for members: the message and a sign-in button that comes back to `path`. */
export function SignInNotice({ path, children }: { path: string; children: ReactNode }) {
  return (
    <div className="notice" role="status">
      <p>{children}</p>
      <a className="btn btn--primary btn--sm" href={loginTo(path)}>
        <Icon name="login" />
        Entrar
      </a>
    </div>
  );
}

/** The message and one small ghost button with its own label ("Limpar filtros"). */
export function ActionNotice({
  label,
  onAction,
  id,
  alert,
  children,
}: {
  label: string;
  onAction: () => void;
  id?: string;
  alert?: boolean;
  children: ReactNode;
}) {
  return (
    <div className="notice" role={alert ? 'alert' : 'status'}>
      <p id={id}>{children}</p>
      <button type="button" className="btn btn--ghost btn--sm" onClick={onAction}>
        {label}
      </button>
    </div>
  );
}

/** The message and one small ghost link to another page ("Ver a agenda"). */
export function LinkNotice({ href, label, children }: { href: string; label: string; children: ReactNode }) {
  return (
    <div className="notice" role="status">
      <p>{children}</p>
      <a className="btn btn--ghost btn--sm" href={href}>
        {label}
      </a>
    </div>
  );
}

/** Loading failed: the message and a "Tentar novamente" button. */
export function RetryNotice({ onRetry, id, alert, children }: { onRetry: () => void; id?: string; alert?: boolean; children: ReactNode }) {
  return (
    <ActionNotice label="Tentar novamente" onAction={onRetry} id={id} alert={alert}>
      {children}
    </ActionNotice>
  );
}
