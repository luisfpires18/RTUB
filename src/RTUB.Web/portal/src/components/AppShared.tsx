// Small hooks and pieces used all over the portal: latest-request guard, session, links, loading.
import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { getCurrentUser, type CurrentUser } from '../data/api';
import { portal } from '../data/content';
import { Icon } from './icons';

// ---------- latest request wins (#226 F14) ----------

/**
 * For a page whose search / filters reload a list: `latest(request, apply)` applies an answer only if no newer request
 * started since, and never after the page is gone - so a slow answer for an old query cannot replace the results of
 * the new one. The dropped answer is simply ignored (no error shown); every real answer, failures included, is applied.
 */
export function useLatest() {
  const last = useRef(0);
  useEffect(() => {
    return () => {
      last.current = -1; // unmounted: nothing applies any more
    };
  }, []);
  return useCallback(<T,>(request: Promise<T>, apply: (value: T) => void) => {
    if (last.current < 0) return;
    const id = ++last.current;
    request.then((value) => {
      if (id === last.current) apply(value);
    });
  }, []);
}

// ---------- session ----------

/** The signed-in state for this page load; `retry` asks the server again. */
export function useCurrentUser() {
  const [state, setState] = useState<{ user?: CurrentUser; failed?: boolean }>({});

  const load = useCallback((refresh: boolean) => {
    setState({});
    getCurrentUser(refresh).then(
      (user) => setState({ user }),
      () => setState({ failed: true }),
    );
  }, []);

  useEffect(() => load(false), [load]);
  return { ...state, retry: () => load(true) };
}

/**
 * The home page's quiet way into the members-only area (the header uses Login and the member shell instead). Always
 * /profile, which explains the area is reserved to RTUB members before offering the login - the portal has no public
 * accounts. Reads `signedOutLabel` for visitors (and while the session is unknown), "A minha conta" once signed in.
 */
export function AccountLink({ className, signedOutLabel }: { className: string; signedOutLabel: string }) {
  const { user } = useCurrentUser();
  const signedIn = user?.authenticated === true;

  return (
    <a className={className} href={portal.profile}>
      <Icon name={signedIn ? 'person' : 'lock'} />
      <span className="member-link__label">{signedIn ? 'A minha conta' : signedOutLabel}</span>
    </a>
  );
}

// ---------- shared pieces ----------

export function ExternalLink({ href, className, children }: { href: string; className?: string; children: ReactNode }) {
  return (
    <a href={href} className={className} target="_blank" rel="noopener noreferrer">
      {children}
      <span className="sr-only"> (abre numa nova janela)</span>
    </a>
  );
}

export function Loading({ label }: { label: string }) {
  return (
    <div className="state" role="status">
      <span className="spinner" aria-hidden="true" />
      <p>{label}</p>
    </div>
  );
}
