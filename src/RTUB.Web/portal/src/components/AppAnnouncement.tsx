// Banner for the system announcement the owner publishes, polled while the page is open.
import { useEffect, useRef, useState } from 'react';
import { getAnnouncement, type Announcement } from '../data/api';
import { Dialog } from './Dialog';

// ---------- system announcement ----------

const ANNOUNCEMENT_KEY = 'rtub-announcement-seen';
const ANNOUNCEMENT_POLL_MS = 30_000;

/**
 * The Owner's system announcement (/owner/tracing), on every portal page, signed in or not.
 * The server keeps only the latest one, for a few minutes, so the page asks every 30 s
 * while it is visible and once more on coming back. Dismissed ids are remembered, so an announcement is shown once
 * per browser (per page load if storage is blocked).
 */
export function SystemAnnouncement() {
  const [current, setCurrent] = useState<Announcement | null>(null);
  const seen = useRef<number | null>(null);

  useEffect(() => {
    try {
      seen.current = Number(localStorage.getItem(ANNOUNCEMENT_KEY)) || null;
    } catch {
      // Storage blocked: remembered for this page only.
    }
    const check = () => {
      if (document.visibilityState !== 'visible') return;
      getAnnouncement().then(
        (a) => a && a.id !== seen.current && setCurrent(a),
        () => {}, // offline or a failed poll: the next one tries again
      );
    };
    check();
    const timer = setInterval(check, ANNOUNCEMENT_POLL_MS);
    document.addEventListener('visibilitychange', check);
    return () => {
      clearInterval(timer);
      document.removeEventListener('visibilitychange', check);
    };
  }, []);

  if (!current) return null;
  const dismiss = () => {
    seen.current = current.id;
    try {
      localStorage.setItem(ANNOUNCEMENT_KEY, String(current.id));
    } catch {
      // Storage blocked: remembered for this page only.
    }
    setCurrent(null);
  };
  return (
    <Dialog
      title="Aviso do sistema"
      onClose={dismiss}
      footer={
        <button type="button" className="btn btn--primary" onClick={dismiss}>
          OK, entendido
        </button>
      }
    >
      <p className="announcement">{current.message}</p>
    </Dialog>
  );
}
