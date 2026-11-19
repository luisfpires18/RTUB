// Session state for the shell and the launch splash reveal.
import { useState } from 'react';
import { wasSignedIn } from '../data/api';
import { type Member } from './MemberShell';
import { useCurrentUser } from './AppShared';

/**
 * Visitor, member, or a member's page still waiting for /api/account/me. "pending" only when the last load was
 * signed in (wasSignedIn), so a member's page opens in the member shell instead of jumping into it; a visitor never
 * waits. A failed session check falls back to the visitor header (with Login).
 */
type Session = { kind: 'visitor' } | { kind: 'pending' } | { kind: 'member'; user: Member };

export function useSession(): Session {
  const { user, failed } = useCurrentUser();
  const [hint] = useState(wasSignedIn);

  if (user?.authenticated) return { kind: 'member', user };
  if (user || failed || !hint) return { kind: 'visitor' };
  return { kind: 'pending' };
}

// ---------- launch splash ----------

const LAUNCH_KEY = 'rtub-portal-launched';
let revealed = false;

/**
 * The splash is static markup in index.html, so it covers the only real wait - the bundle
 * loading - and costs nothing extra. Once React has committed, the first load of a session (every
 * PWA launch is a new session) plays the opening on the home page (styles.css, "opening") and lifts
 * like a stage curtain anywhere else; later loads, or reduced motion, remove it instantly. A click,
 * tap or key skips the opening. No timers: the splash goes when its last animation ends.
 */
export function revealApp() {
  const splash = document.getElementById('splash');
  if (revealed || !splash) return;
  revealed = true;

  let firstLoad = true;
  try {
    firstLoad = sessionStorage.getItem(LAUNCH_KEY) === null;
    sessionStorage.setItem(LAUNCH_KEY, '1');
  } catch {
    // Storage blocked (private mode, policy): treat as a first load.
  }

  if (!firstLoad || matchMedia('(prefers-reduced-motion: reduce)').matches) {
    splash.remove();
    return;
  }

  const root = document.documentElement;
  // Skipping also drops .intro, so the hero shows at once instead of waiting for a curtain that never wipes.
  const skip = () => {
    root.classList.remove('intro');
    splash.remove();
  };
  if (location.pathname === '/') {
    root.classList.add('intro');
    splash.addEventListener('pointerdown', skip, { once: true });
    window.addEventListener('keydown', skip, { once: true });
  } else {
    splash.classList.add('splash--exit');
  }
  Promise.allSettled(splash.getAnimations({ subtree: true }).map((a) => a.finished)).then(() => {
    window.removeEventListener('keydown', skip);
    splash.remove();
  });
}
