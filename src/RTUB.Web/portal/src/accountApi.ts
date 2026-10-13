import { call } from './eventsApi';

// /api/account/forgot-password, /reset-password and /confirm-email (Endpoints/AccountRecoveryEndpoints.cs, task 036).
// Public; every call is a POST with the X-CSRF-TOKEN header (call()). Tokens go in the body, never in a URL we build.

export type RecoveryResult = { succeeded: boolean; message: string };

export const accountApi = {
  forgotPassword: (email: string) => call<RecoveryResult>('POST', '/api/account/forgot-password', { email }),
  resetPassword: (email: string, code: string, password: string, confirmPassword: string) =>
    call<RecoveryResult>('POST', '/api/account/reset-password', { email, code, password, confirmPassword }),
  confirmEmail: (userId: string | null, code: string | null) => call<RecoveryResult>('POST', '/api/account/confirm-email', { userId, code }),
};

/**
 * Reads the link's query once and drops it from the address bar and history, so the token is not left on screen, in
 * the history or in a bookmark. Kept in memory only (never storage); the caller forgets it once used.
 */
export function takeLinkQuery<K extends string>(...names: K[]): Record<K, string | null> {
  const query = new URLSearchParams(location.search);
  const values = Object.fromEntries(names.map((n) => [n, query.get(n)])) as Record<K, string | null>;
  if (location.search) history.replaceState(null, '', location.pathname);
  return values;
}
