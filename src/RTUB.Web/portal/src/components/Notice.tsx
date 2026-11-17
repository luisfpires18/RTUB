import type { ReactNode } from 'react';
import { loginTo } from '../data/content';
import { Icon } from './icons';

// The short message a page shows where its content would be. Use these instead of writing the markup again.

/** Just the message. */
export function Notice({ children }: { children: ReactNode }) {
  return (
    <div className="notice" role="status">
      <p>{children}</p>
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

/** Loading failed: the message and a "Tentar novamente" button. */
export function RetryNotice({ onRetry, children }: { onRetry: () => void; children: ReactNode }) {
  return (
    <div className="notice" role="status">
      <p>{children}</p>
      <button type="button" className="btn btn--ghost btn--sm" onClick={onRetry}>
        Tentar novamente
      </button>
    </div>
  );
}
