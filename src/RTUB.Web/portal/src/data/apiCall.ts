// The shared request helper: every answer maps onto an Outcome, nothing throws.

export const json = <T,>(r: Response) => (r.ok ? (r.json() as Promise<T>) : Promise.reject(new Error(`HTTP ${r.status}`)));

// ---------- the shared request helper (every answer maps onto an Outcome; nothing throws) ----------
// It lives in its own small module because the shell mounts the unread badge on every member page: importing `call`
// from a page client would pull that whole client into the entry chunk.

/** Every answer maps onto one of these; nothing throws. */
export type Outcome<T> =
  | { kind: 'ok'; data: T }
  /** `title` is the server's summary sentence (Música shows it). */
  | { kind: 'invalid'; errors: Record<string, string>; title?: string }
  | { kind: 'signin' }
  | { kind: 'closed' }
  /** `unavailable`: the thing exists but its file does not (a Música song without audio). */
  | { kind: 'notfound'; unavailable?: boolean }
  | { kind: 'forbidden' }
  | { kind: 'inuse' }
  /** 409 from a Logística card saved from an older copy; an archived board or card is 'closed'. */
  | { kind: 'conflict' }
  | { kind: 'failed'; title?: string };

/**
 * The sentence a page shows when a call did not work. `what` finishes "Não foi possível …" ("guardar", "apagar a
 * pasta"); `messages` holds the area's own words for a kind (forbidden, notfound, closed, conflict, ...). A form
 * error shows the server's first field message.
 */
export function outcomeText(o: Outcome<unknown>, what: string, messages: Partial<Record<Outcome<unknown>['kind'], string>> = {}) {
  const own = messages[o.kind];
  if (own !== undefined) return own;
  if (o.kind === 'invalid') return Object.values(o.errors)[0];
  if (o.kind === 'signin') return 'A sessão terminou. Entra outra vez.';
  return `Não foi possível ${what}. Tenta outra vez daqui a pouco.`;
}

let token: Promise<string> | null = null;

/** One antiforgery token per page load (it is bound to the session). */
export function antiforgeryToken(refresh = false): Promise<string> {
  if (!token || refresh) {
    token = fetch('/api/public/antiforgery-token', { credentials: 'same-origin' })
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error(`HTTP ${r.status}`))))
      .then((t: { token: string }) => t.token)
      .catch((e) => {
        token = null;
        throw e;
      });
  }
  return token;
}

/** { q: 'ana', page: '' } → "?q=ana" (empty values left out); "" when nothing is left. */
export const queryString = (params: Record<string, string>) => {
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v) p.set(k, v);
  const s = p.toString();
  return s ? `?${s}` : '';
};

export async function call<T>(method: string, url: string, body?: unknown, retried = false): Promise<Outcome<T>> {
  try {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (method !== 'GET') headers['X-CSRF-TOKEN'] = await antiforgeryToken();
    if (body !== undefined && !(body instanceof FormData)) headers['Content-Type'] = 'application/json';

    const r = await fetch(url, {
      method,
      headers,
      body: body === undefined || body instanceof FormData ? body : JSON.stringify(body),
      credentials: 'same-origin',
    });
    if (r.status === 204) return { kind: 'ok', data: undefined as T };
    if (r.ok) return { kind: 'ok', data: (await r.json()) as T };

    const problem = (await r.json().catch(() => ({}))) as { errors?: Record<string, string[]>; type?: string; title?: string };
    if (r.status === 400 && problem.errors) {
      const errors: Record<string, string> = {};
      for (const [k, v] of Object.entries(problem.errors)) errors[k.charAt(0).toLowerCase() + k.slice(1)] = v[0];
      return { kind: 'invalid', errors, title: problem.title };
    }
    // A refused antiforgery token (expired page, new session): fetch a fresh one and try once more.
    if (r.status === 400 && method !== 'GET' && !retried) {
      await antiforgeryToken(true);
      return call<T>(method, url, body, true);
    }
    if (r.status === 401) return { kind: 'signin' };
    if (r.status === 404) return { kind: 'notfound', unavailable: problem.type === 'music:audio-unavailable' };
    if (r.status === 403) return { kind: 'forbidden' };
    if (r.status === 409) return { kind: problem.type === 'events:in-use' ? 'inuse' : problem.type === 'logistics:conflict' ? 'conflict' : 'closed' };
    return { kind: 'failed', title: problem.title };
  } catch {
    return { kind: 'failed' };
  }
}
