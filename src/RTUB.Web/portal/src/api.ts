// Typed contracts with the ASP.NET Core host. See docs/react-portal-pilot.md.

/** GET /api/account/me (AccountController). Only the caller's own, non-sensitive fields. */
export type CurrentUser =
  | { authenticated: false }
  | {
      authenticated: true;
      displayName: string;
      fullName: string | null;
      avatarUrl: string;
      categories: string[];
    };

let session: Promise<CurrentUser> | null = null;

/** One request per page load, shared by the header and the profile page; `refresh` retries. */
export function getCurrentUser(refresh = false): Promise<CurrentUser> {
  if (!session || refresh) {
    session = fetch('/api/account/me', { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
      .then((r) => (r.ok ? (r.json() as Promise<CurrentUser>) : Promise.reject(new Error(`HTTP ${r.status}`))))
      .catch((e) => {
        session = null; // let the next caller try again
        throw e;
      });
  }
  return session;
}

let version: Promise<string> | null = null;

/** GET /api/version, once per page load: the footer shows it, and it tells test builds apart. */
export function getVersion(): Promise<string> {
  version ??= fetch('/api/version', { headers: { Accept: 'application/json' } })
    .then((r) => (r.ok ? r.json() : Promise.reject(new Error(`HTTP ${r.status}`))))
    .then((body: { version?: unknown }) =>
      typeof body.version === 'string' ? body.version : Promise.reject(new Error('no version')),
    );
  return version;
}

/** The React request form's fields (names match POST /api/public/requests). Dates are yyyy-MM-dd. */
export type RequestForm = {
  name: string;
  email: string;
  phone: string;
  eventType: string;
  preferredDate: string;
  isDateRange: boolean;
  preferredEndDate: string;
  location: string;
  message: string;
  website: string; // honeypot - always empty for people
};

export type FieldErrors = Partial<Record<keyof RequestForm, string>>;

export type SubmitOutcome =
  | { kind: 'submitted' }
  | { kind: 'invalid'; errors: FieldErrors }
  | { kind: 'expired' } // antiforgery token refused: reload and retry
  | { kind: 'throttled' }
  | { kind: 'failed' };

/**
 * GET /api/public/antiforgery-token, then POST /api/public/requests as a form (the server enforces
 * antiforgery on form posts). Every server answer maps to one outcome; nothing throws.
 */
export async function submitRequest(form: RequestForm): Promise<SubmitOutcome> {
  try {
    const tokenResponse = await fetch('/api/public/antiforgery-token', { credentials: 'same-origin' });
    if (!tokenResponse.ok) return { kind: 'failed' };
    const { fieldName, token } = (await tokenResponse.json()) as { fieldName: string; token: string };

    const body = new FormData();
    body.set(fieldName, token);
    for (const [key, value] of Object.entries(form)) {
      if (key === 'preferredEndDate' && !form.isDateRange) continue;
      body.set(key, String(value));
    }

    const response = await fetch('/api/public/requests', { method: 'POST', body, credentials: 'same-origin' });
    if (response.ok) return { kind: 'submitted' };
    if (response.status === 429) return { kind: 'throttled' };
    if (response.status === 400) {
      const problem = await response.json().catch(() => null);
      if (!problem?.errors) return { kind: 'expired' };
      const errors: FieldErrors = {};
      for (const [field, messages] of Object.entries(problem.errors as Record<string, string[]>)) {
        errors[field as keyof RequestForm] = messages[0];
      }
      return { kind: 'invalid', errors };
    }
    return { kind: 'failed' };
  } catch {
    return { kind: 'failed' };
  }
}
