import type { SignInOutcome } from './api';
import { antiforgeryToken, call } from './eventsApi';

/**
 * Face ID / fingerprint sign-in (task 057): WebAuthn passkeys on ASP.NET Core Identity. The server hands out the
 * options as WebAuthn JSON and takes the credential back as JSON; this file only converts between that JSON and the
 * browser API (natively where the browser can, by hand for older Safari). Nothing throws.
 */

type Json = Record<string, unknown>;
type Pkc = typeof PublicKeyCredential & {
  parseCreationOptionsFromJSON?: (o: Json) => PublicKeyCredentialCreationOptions;
  parseRequestOptionsFromJSON?: (o: Json) => PublicKeyCredentialRequestOptions;
};
type Credential = PublicKeyCredential & { toJSON?: () => Json };

/** The device can make and use passkeys here (secure context + WebAuthn). */
export const passkeysSupported = () => typeof window !== 'undefined' && window.isSecureContext && 'PublicKeyCredential' in window;

const bytes = (s: string) =>
  Uint8Array.from(atob(s.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(s.length / 4) * 4, '=')), (c) => c.charCodeAt(0));
const text = (b: ArrayBuffer | null | undefined) =>
  b ? btoa(String.fromCharCode(...new Uint8Array(b))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '') : null;
const withIds = (list: unknown) => ((list as Json[] | undefined) ?? []).map((c) => ({ ...c, id: bytes(c.id as string) }));

function creationOptions(o: Json): PublicKeyCredentialCreationOptions {
  const pkc = PublicKeyCredential as Pkc;
  if (pkc.parseCreationOptionsFromJSON) return pkc.parseCreationOptionsFromJSON(o);
  const user = o.user as Json;
  return {
    ...o,
    challenge: bytes(o.challenge as string),
    user: { ...user, id: bytes(user.id as string) },
    excludeCredentials: withIds(o.excludeCredentials),
  } as unknown as PublicKeyCredentialCreationOptions;
}

function requestOptions(o: Json): PublicKeyCredentialRequestOptions {
  const pkc = PublicKeyCredential as Pkc;
  if (pkc.parseRequestOptionsFromJSON) return pkc.parseRequestOptionsFromJSON(o);
  return { ...o, challenge: bytes(o.challenge as string), allowCredentials: withIds(o.allowCredentials) } as unknown as PublicKeyCredentialRequestOptions;
}

function toJson(c: Credential): string {
  if (c.toJSON) return JSON.stringify(c.toJSON());
  const made = c.response as AuthenticatorAttestationResponse;
  const used = c.response as AuthenticatorAssertionResponse;
  const response =
    'attestationObject' in c.response
      ? { clientDataJSON: text(made.clientDataJSON), attestationObject: text(made.attestationObject), transports: made.getTransports?.() ?? [] }
      : { clientDataJSON: text(used.clientDataJSON), authenticatorData: text(used.authenticatorData), signature: text(used.signature), userHandle: text(used.userHandle) };
  return JSON.stringify({
    id: c.id,
    rawId: text(c.rawId),
    type: c.type,
    authenticatorAttachment: c.authenticatorAttachment,
    clientExtensionResults: c.getClientExtensionResults(),
    response,
  });
}

/** 'cancelled' when the person closed the prompt (or it timed out); 'exists' when this device already has one. */
type DeviceFailure = 'cancelled' | 'exists' | 'failed';
const deviceFailure = (e: unknown): DeviceFailure =>
  e instanceof DOMException && e.name === 'NotAllowedError' ? 'cancelled' : e instanceof DOMException && e.name === 'InvalidStateError' ? 'exists' : 'failed';

/** POST /auth/passkey/options → device prompt → POST /auth/passkey/login. Same outcomes as the password sign-in. */
export async function signInWithPasskey(rememberMe: boolean, returnUrl: string): Promise<SignInOutcome | { kind: 'cancelled' }> {
  const options = await call<Json>('POST', '/auth/passkey/options');
  if (options.kind !== 'ok') return { kind: options.kind === 'invalid' ? 'expired' : 'failed' };

  let credential: Credential;
  try {
    credential = (await navigator.credentials.get({ publicKey: requestOptions(options.data) })) as Credential;
  } catch (e) {
    return { kind: deviceFailure(e) === 'cancelled' ? 'cancelled' : 'failed' };
  }

  try {
    const r = await fetch('/auth/passkey/login', {
      method: 'POST',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json', 'X-CSRF-TOKEN': await antiforgeryToken() },
      body: JSON.stringify({ credential: toJson(credential), rememberMe, returnUrl }),
      credentials: 'same-origin',
    });
    if (r.ok) return { kind: 'signedIn', redirect: ((await r.json()) as { redirect: string }).redirect };
    if (r.status === 401) {
      const { error } = (await r.json().catch(() => ({}))) as { error?: string };
      return { kind: error === 'Locked' ? 'locked' : error === 'Expelled' ? 'expelled' : 'passkey' };
    }
    if (r.status === 429) return { kind: 'throttled' };
    if (r.status === 400) return { kind: 'expired' };
    return { kind: 'failed' };
  } catch {
    return { kind: 'failed' };
  }
}

/** One passkey of GET /api/me/passkeys. `id` is the credential id (base64url). */
export type MyPasskey = { id: string; name: string; createdAt: string };

export const passkeyApi = {
  list: () => call<MyPasskey[]>('GET', '/api/me/passkeys'),
  remove: (id: string) => call<void>('DELETE', `/api/me/passkeys/${encodeURIComponent(id)}`),
  /** POST /api/me/passkeys/options (the current password) → device prompt → POST /api/me/passkeys. */
  async add(
    currentPassword: string,
  ): Promise<{ kind: 'ok'; data: MyPasskey } | { kind: 'invalid'; errors: Record<string, string> } | { kind: DeviceFailure | 'signin' | 'full' }> {
    const options = await call<Json>('POST', '/api/me/passkeys/options', { currentPassword });
    if (options.kind === 'invalid') return options;
    if (options.kind !== 'ok') return { kind: options.kind === 'signin' ? 'signin' : options.kind === 'closed' ? 'full' : 'failed' };

    let credential: Credential;
    try {
      credential = (await navigator.credentials.create({ publicKey: creationOptions(options.data) })) as Credential;
    } catch (e) {
      return { kind: deviceFailure(e) };
    }

    const saved = await call<MyPasskey>('POST', '/api/me/passkeys', { credential: toJson(credential) });
    return saved.kind === 'ok' ? saved : { kind: saved.kind === 'signin' ? 'signin' : saved.kind === 'closed' ? 'full' : 'failed' };
  },
};
