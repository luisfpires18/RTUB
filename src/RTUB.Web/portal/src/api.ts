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

/**
 * PLANNED - not called yet. The body the future `POST /api/requests` will take, mirroring what the
 * Blazor /request page sends to RequestService.CreateRequestAsync today (limits from
 * RTUB.Core.Entities.Request, date rules from RequestValidationService). Submissions stay on the
 * Blazor page until that endpoint exists with antiforgery, rate limiting and anti-spam; see the
 * "Request migration" plan in docs/react-portal-pilot.md.
 */
export type RequestSubmission = {
  name: string; // required, ≤ 200
  email: string; // required, valid email, ≤ 200
  phone: string; // required, ≤ 20
  eventType: string; // required, ≤ 100
  preferredDate: string; // yyyy-MM-dd, not in the past
  preferredEndDate?: string; // date range only: not in the past, ≥ preferredDate
  location: string; // required, ≤ 200
  message?: string; // ≤ 2000
};
