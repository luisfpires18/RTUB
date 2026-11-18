// Small hooks and pieces used all over the portal: latest-request guard, session, links, loading.
import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { getCurrentUser, type CurrentUser, type Outcome } from '../data/api';
import { portal } from '../data/content';
import { Icon } from './icons';

// ---------- latest request wins ----------

/**
 * For a page whose search / filters reload a list: `latest(request, apply)` applies an answer only if no newer request
 * started since, and never after the page is gone - so a slow answer for an old query cannot replace the results of
 * the new one. The dropped answer is simply ignored (no error shown); every real answer, failures included, is applied.
 */
export function useLatest() {
  const last = useRef(0);
  useEffect(() => {
    last.current = 0; // mounted again (StrictMode's test remount in development)
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

/**
 * Loads on mount and whenever `deps` change, and keeps `pick(answer)` (a picker from `data/apiCall.ts`). An answer
 * that arrives after the inputs changed again, or after the page is gone, is dropped. `reload()` asks again and keeps
 * the old value on screen meanwhile; `set` replaces the value (after a save).
 */
export function useLoad<T, S>(load: () => Promise<Outcome<T>>, deps: unknown[], pick: (o: Outcome<T>) => S): [S | undefined, () => void, (next: S) => void] {
  const [state, setState] = useState<S>();
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let live = true;
    load().then((o) => {
      if (live) setState(pick(o));
    });
    return () => {
      live = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, version]);

  return [state, () => setVersion((v) => v + 1), setState];
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
