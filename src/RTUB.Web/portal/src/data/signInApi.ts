// Sign in and sign out against the account endpoints.
import { rememberMemberShell } from './apiSession';

/** 'passkey': the device's passkey is unknown here (removed in /profile, or made on another site) - task 057. */
export type SignInFailure = 'invalid' | 'locked' | 'expelled' | 'throttled' | 'expired' | 'failed' | 'passkey';
export type SignInOutcome = { kind: 'signedIn'; redirect: string } | { kind: SignInFailure };

const signInErrors: Record<string, SignInFailure> = { Invalid: 'invalid', Locked: 'locked', Expelled: 'expelled' };

/**
 * Signs out through the existing POST /auth/logout (Program.cs): a plain form post with the antiforgery
 * token, so the browser follows its redirect to / like a regular form post. False when the
 * token could not be fetched; nothing is sent then.
 */
export async function signOut(): Promise<boolean> {
  try {
    const tokenResponse = await fetch('/api/public/antiforgery-token', { credentials: 'same-origin' });
    if (!tokenResponse.ok) return false;
    const { fieldName, token } = (await tokenResponse.json()) as { fieldName: string; token: string };
    rememberMemberShell(false);
    const form = document.createElement('form');
    form.method = 'post';
    form.action = '/auth/logout';
    const field = document.createElement('input');
    field.type = 'hidden';
    field.name = fieldName;
    field.value = token;
    form.append(field);
    document.body.append(form);
    form.submit();
    return true;
  } catch {
    return false;
  }
}

/**
 * The existing POST /auth/login (Program.cs): same form fields, antiforgery and per-IP limit as
 * a regular login form. Accept: application/json makes it answer { redirect } or 401 { error }
 * instead of redirecting. The server alone validates the return URL. Nothing throws.
 */
export async function signIn(username: string, password: string, rememberMe: boolean, returnUrl: string): Promise<SignInOutcome> {
  try {
    const tokenResponse = await fetch('/api/public/antiforgery-token', { credentials: 'same-origin' });
    if (!tokenResponse.ok) return { kind: 'failed' };
    const { fieldName, token } = (await tokenResponse.json()) as { fieldName: string; token: string };

    const body = new FormData();
    body.set(fieldName, token);
    body.set('Username', username);
    body.set('Password', password);
    body.set('RememberMe', String(rememberMe));
    body.set('ReturnUrl', returnUrl);

    const response = await fetch('/auth/login', {
      method: 'POST',
      body,
      headers: { Accept: 'application/json' },
      credentials: 'same-origin',
    });
    if (response.ok) {
      const { redirect } = (await response.json()) as { redirect: string };
      return { kind: 'signedIn', redirect };
    }
    if (response.status === 401) {
      const { error } = (await response.json().catch(() => ({}))) as { error?: string };
      return { kind: signInErrors[error ?? ''] ?? 'invalid' };
    }
    if (response.status === 429) return { kind: 'throttled' };
    if (response.status === 400) return { kind: 'expired' };
    return { kind: 'failed' };
  } catch {
    return { kind: 'failed' };
  }
}
