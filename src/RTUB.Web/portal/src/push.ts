import { call, type Outcome } from './eventsApi';

// Web Push for the React /profile (task 038, docs/react-push-v2.md): /api/push (Endpoints/PushEndpoints.cs) plus the
// browser's own state. Capability detection only, no user-agent checks. Nothing is stored in the browser: the server
// and the browser's PushSubscription are the two sources, compared on every read. Permission is asked, and a
// subscription made, only from a click.

/** The server's side (GET /api/push/status). reason: 'disabled' | 'not-configured' | null. */
export type PushStatus = {
  enabled: boolean;
  configured: boolean;
  reason: string | null;
  vapidPublicKey: string | null;
  optedOut: boolean;
  subscriptions: number;
};

export const pushApi = {
  status: () => call<PushStatus>('GET', '/api/push/status'),
  check: (endpoint: string) => call<{ registered: boolean }>('POST', '/api/push/check', { endpoint }),
  subscribe: (subscription: PushSubscriptionJSON, replaces: string | null) =>
    call<PushStatus>('POST', '/api/push/subscribe', {
      endpoint: subscription.endpoint,
      keys: subscription.keys,
      expirationTime: subscription.expirationTime ?? null,
      replaces,
    }),
  unsubscribe: (endpoint: string | null, all: boolean) => call<PushStatus>('POST', '/api/push/unsubscribe', { endpoint, all }),
};

/**
 * What /profile shows. "others" = the member's subscribed devices other than this one.
 * - unsupported: no Service Worker / Push API / Notification here (installed: running as an installed app)
 * - disabled / not-configured: the server's reason
 * - no-worker: the service worker never became ready
 * - denied: notifications blocked for the site (registered: this browser's old subscription is still stored)
 * - off: no subscription in this browser
 * - on: subscribed here and stored for the member
 * - unregistered: subscribed here, unknown to the server
 * - stale: subscribed here with an old server key, or expired
 * - error: the status could not be read
 */
export type PushState =
  | { kind: 'unsupported'; installed: boolean }
  | { kind: 'disabled' | 'not-configured' | 'no-worker'; status: PushStatus }
  | { kind: 'denied'; status: PushStatus; registered: boolean; others: number }
  | { kind: 'off'; status: PushStatus; others: number }
  | { kind: 'on' | 'unregistered' | 'stale'; status: PushStatus; others: number }
  | { kind: 'error' };

export function pushSupported(): boolean {
  return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
}

/** Running as an installed app (some browsers, e.g. on iPhone and iPad, offer push only there). */
export function runningInstalled(): boolean {
  return window.matchMedia('(display-mode: standalone)').matches || (navigator as { standalone?: boolean }).standalone === true;
}

/** The active registration sw-register.js made (the only registration path), or null if it is not ready in time. */
async function registration(timeoutMs = 8000): Promise<ServiceWorkerRegistration | null> {
  const timeout = new Promise<null>((resolve) => setTimeout(() => resolve(null), timeoutMs));
  return Promise.race([navigator.serviceWorker.ready, timeout]);
}

export function urlBase64ToUint8Array(value: string): Uint8Array<ArrayBuffer> {
  const base64 = (value + '='.repeat((4 - (value.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/');
  const raw = atob(base64);
  const bytes = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
  return bytes;
}

/** False when the subscription was made with another server key (rotated VAPID keys); unknown counts as the same. */
function sameKey(subscription: PushSubscription, vapidPublicKey: string | null): boolean {
  const used = subscription.options?.applicationServerKey;
  if (!used || !vapidPublicKey) return true;
  const a = new Uint8Array(used);
  const b = urlBase64ToUint8Array(vapidPublicKey);
  return a.length === b.length && a.every((x, i) => x === b[i]);
}

const expired = (subscription: PushSubscription) =>
  subscription.expirationTime !== null && subscription.expirationTime <= Date.now();

/** Reads both sides and compares them. Never asks for permission and never subscribes. */
export async function readPush(): Promise<PushState> {
  if (!pushSupported()) return { kind: 'unsupported', installed: runningInstalled() };

  const s = await pushApi.status();
  if (s.kind !== 'ok') return { kind: 'error' };
  const status = s.data;
  if (status.reason === 'disabled' || !status.enabled) return { kind: 'disabled', status };
  if (status.reason === 'not-configured' || !status.vapidPublicKey) return { kind: 'not-configured', status };

  try {
    const reg = await registration();
    if (!reg) return { kind: 'no-worker', status };

    const subscription = await reg.pushManager.getSubscription();
    let registered = false;
    if (subscription) {
      const c = await pushApi.check(subscription.endpoint);
      if (c.kind !== 'ok') return { kind: 'error' };
      registered = c.data.registered;
    }
    const others = Math.max(0, status.subscriptions - (registered ? 1 : 0));

    if (Notification.permission === 'denied') return { kind: 'denied', status, registered, others };
    if (!subscription) return { kind: 'off', status, others };
    if (!sameKey(subscription, status.vapidPublicKey) || expired(subscription)) return { kind: 'stale', status, others };
    return { kind: registered ? 'on' : 'unregistered', status, others };
  } catch {
    return { kind: 'error' };
  }
}

export type PushResult = { state: PushState; error?: string };

function failure(o: Outcome<unknown>): string {
  if (o.kind === 'invalid') return Object.values(o.errors)[0] ?? 'O navegador deu uma subscrição inválida.';
  if (o.kind === 'signin') return 'A sessão terminou. Entra outra vez.';
  if (o.kind === 'forbidden') return 'As notificações push ainda não estão disponíveis.';
  if (o.kind === 'closed') return 'As notificações push não estão configuradas neste servidor.';
  return 'Não foi possível guardar. Tenta outra vez daqui a pouco.';
}

/**
 * Turns push on in this browser, or repairs it (stale or unknown to the server). Call it straight from a click:
 * the permission prompt comes first, before any network request, so browsers that need the click still see it.
 */
export async function turnOn(vapidPublicKey: string): Promise<PushResult> {
  try {
    const permission = Notification.permission === 'granted' ? 'granted' : await Notification.requestPermission();
    if (permission !== 'granted') return { state: await readPush() };

    const reg = await navigator.serviceWorker.ready;
    let subscription = await reg.pushManager.getSubscription();
    let replaces: string | null = null;
    if (subscription && (!sameKey(subscription, vapidPublicKey) || expired(subscription))) {
      replaces = subscription.endpoint;
      await subscription.unsubscribe();
      subscription = null;
    }
    subscription ??= await reg.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(vapidPublicKey),
    });

    const o = await pushApi.subscribe(subscription.toJSON(), replaces);
    return o.kind === 'ok' ? { state: await readPush() } : { state: await readPush(), error: failure(o) };
  } catch {
    return { state: await readPush(), error: 'O navegador não conseguiu ativar as notificações. Tenta outra vez daqui a pouco.' };
  }
}

/** Turns push off in this browser (all: on every device of the member). The server first, then the browser. */
export async function turnOff(all: boolean): Promise<PushResult> {
  try {
    const reg = await navigator.serviceWorker.getRegistration();
    const subscription = reg ? await reg.pushManager.getSubscription() : null;
    if (all || subscription) {
      const o = await pushApi.unsubscribe(subscription?.endpoint ?? null, all);
      if (o.kind !== 'ok') return { state: await readPush(), error: failure(o) };
    }
    await subscription?.unsubscribe();
    return { state: await readPush() };
  } catch {
    return { state: await readPush(), error: 'Não foi possível desativar. Tenta outra vez daqui a pouco.' };
  }
}
