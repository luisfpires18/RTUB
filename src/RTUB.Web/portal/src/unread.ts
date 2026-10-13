import { useSyncExternalStore } from 'react';
import { call } from './eventsApi';

// The header's unread badge (task 044). One authoritative number: GET /api/conversations/unread, the sum of the
// list's per-conversation unreadCount (unread MESSAGES, not conversations). Messaging v2 has no push channel to the
// page (no hub since 037), so the shell polls while the tab is visible and again on coming back; /messages
// publishes the sum it already loaded, so the badge follows the list without a second request. A page load starts
// from the server's number, so a reload or a reconnect always reconciles.
const POLL_MS = 10_000;

let count = 0;
const listeners = new Set<() => void>();

function publish(n: number) {
  if (n === count) return;
  count = n;
  listeners.forEach((l) => l());
}

/** The total the server just reported (the /messages list). */
export const publishUnread = (conversations: { unreadCount: number }[]) =>
  publish(conversations.reduce((sum, c) => sum + c.unreadCount, 0));

/** Ask the server again now. A failed or signed-out check keeps the last number. */
export async function refreshUnread() {
  const o = await call<{ unreadCount: number }>('GET', '/api/conversations/unread');
  if (o.kind === 'ok') publish(o.data.unreadCount);
}

/** "3", "99+", or nothing at 0. */
export const unreadLabel = (n: number) => (n <= 0 ? '' : n > 99 ? '99+' : String(n));

function subscribe(listener: () => void) {
  listeners.add(listener);
  if (listeners.size > 1) return () => void listeners.delete(listener);

  const tick = () => document.visibilityState === 'visible' && void refreshUnread();
  tick();
  const timer = setInterval(tick, POLL_MS);
  document.addEventListener('visibilitychange', tick);
  addEventListener('online', tick);
  return () => {
    listeners.delete(listener);
    clearInterval(timer);
    document.removeEventListener('visibilitychange', tick);
    removeEventListener('online', tick);
  };
}

/** The member's unread message count; polling runs while at least one component uses it. */
export const useUnread = () =>
  useSyncExternalStore(subscribe, () => count, () => 0);
