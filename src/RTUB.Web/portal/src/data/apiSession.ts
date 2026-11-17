// The signed-in state (GET /api/account/me), the member-shell hint and the system announcement.


/**
 * Which member-menu groups the caller sees (MemberMenuAccess): the navbar rules, decided by the
 * server. It only hides links; every page and API still enforces its own rule.
 */
export type MemberMenu = { management: boolean; logistics: boolean; treasury: boolean; admin: boolean; owner: boolean };

/**
 * GET /api/account/me (AccountController). Only the caller's own, non-sensitive fields. `version` is the build, as
 * /api/version reports it: the footer reads it from here, so a page load makes one request fewer (task 054).
 */
export type CurrentUser =
  | { authenticated: false; version: string }
  | {
      authenticated: true;
      version: string;
      displayName: string;
      fullName: string | null;
      avatarUrl: string;
      categories: string[];
      menu: MemberMenu;
      /** Reformado (task 087): no "As minhas ações pendentes" entry or page. */
      retired: boolean;
      /** Calendar:PublicOrigin (task 079), the trusted origin for links that leave RTUB; null when not configured. */
      publicOrigin: string | null;
    };

let session: Promise<CurrentUser> | null = null;

/** One request per page load, shared by the header and the profile page; `refresh` retries. */
export function getCurrentUser(refresh = false): Promise<CurrentUser> {
  if (!session || refresh) {
    session = fetch('/api/account/me', { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
      .then((r) => (r.ok ? (r.json() as Promise<CurrentUser>) : Promise.reject(new Error(`HTTP ${r.status}`))))
      .then((user) => {
        rememberMemberShell(user.authenticated);
        return user;
      })
      .catch((e) => {
        session = null; // let the next caller try again
        throw e;
      });
  }
  return session;
}

const SHELL_HINT = 'rtub-member-shell';

/**
 * Whether the last page load was signed in. Only a layout hint, so a member's page opens with the member shell
 * instead of jumping into it once /api/account/me answers; the answer always wins. Never trusted for anything else.
 */
export function wasSignedIn(): boolean {
  try {
    return localStorage.getItem(SHELL_HINT) === '1';
  } catch {
    return false; // storage blocked: start as a visitor
  }
}

export function rememberMemberShell(signedIn: boolean) {
  try {
    if (signedIn) localStorage.setItem(SHELL_HINT, '1');
    else localStorage.removeItem(SHELL_HINT);
  } catch {
    // Storage blocked: the shell simply waits for the session on each load.
  }
}

/** GET /api/announcement (task 037): the Owner's system announcement while it is fresh, or null (204). */
export type Announcement = { id: number; message: string };

export function getAnnouncement(): Promise<Announcement | null> {
  return fetch('/api/announcement', { headers: { Accept: 'application/json' }, cache: 'no-store' }).then((r) =>
    r.status === 204 ? null : r.ok ? (r.json() as Promise<Announcement>) : Promise.reject(new Error(`HTTP ${r.status}`)),
  );
}
