import { useEffect, useState, type ReactNode } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { pushApi, readPush, turnOn, type PushState } from './push';
import { reminderDue, reminderFits } from './startupRules';

// Task 080 (#261; docs/react-push-v2.md → Startup reminder). The startup queue: one dialog at a time, in order, once
// per app opening, for signed-in members only. #264's release notes go first in STEPS; the notification reminder
// follows. Every page is its own document (no client router), so "an app opening" is this tab's session: a new tab or
// window, or a cold launch of the installed app, starts one; moving between pages, coming back to the app, polls and
// re-renders do not. The visitor shell clears it (sign-out, and /login before signing in or switching account).
// SystemAnnouncement keeps its own purpose and is not part of the queue.

const LAUNCH_KEY = 'rtub-startup-done';
const SETTLE_MS = 1500;

type Step = (props: { onDone: () => void }) => ReactNode;
const STEPS: Step[] = [PushReminder];

/** Forget this launch's steps (called by the visitor shell). */
export function resetStartup() {
  try {
    sessionStorage.removeItem(LAUNCH_KEY);
  } catch {
    // storage blocked: nothing was remembered
  }
}

function launchDone(id: string): boolean {
  try {
    return (sessionStorage.getItem(LAUNCH_KEY) ?? '').split(',').includes(id);
  } catch {
    return false;
  }
}

function markLaunchDone(id: string) {
  try {
    const done = (sessionStorage.getItem(LAUNCH_KEY) ?? '').split(',').filter(Boolean);
    if (!done.includes(id)) sessionStorage.setItem(LAUNCH_KEY, [...done, id].join(','));
  } catch {
    // storage blocked: this page only
  }
}

/**
 * Waits for the page to settle (a dialog the page opened itself, e.g. from a notification's deep link, goes first) and
 * for no other dialog to be open, then runs the steps in order. Never stacks on another dialog.
 */
export function StartupModals() {
  const [step, setStep] = useState(-1);

  useEffect(() => {
    let timer = 0;
    const wait = () => {
      if (document.visibilityState === 'visible' && !document.querySelector('dialog[open]')) setStep(0);
      else timer = window.setTimeout(wait, 1000);
    };
    timer = window.setTimeout(wait, SETTLE_MS);
    return () => clearTimeout(timer);
  }, []);

  if (step < 0 || step >= STEPS.length) return null;
  const Current = STEPS[step];
  return <Current key={step} onDone={() => setStep((s) => s + 1)} />;
}

// ---------- notification reminder ----------

const REMINDER = 'push';

function PushReminder({ onDone }: { onDone: () => void }) {
  const [state, setState] = useState<PushState | null>(null);
  const [reformed, setReformed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (launchDone(REMINDER)) return onDone();
    let live = true;
    Promise.all([pushApi.reminder(), readPush()]).then(
      ([account, device]) => {
        if (!live) return;
        if (account.kind !== 'ok' || device.kind === 'error') return onDone(); // unknown: try again on the next page
        if (!reminderFits(device) || !reminderDue(account.data)) {
          markLaunchDone(REMINDER);
          return onDone();
        }
        setReformed(account.data.reformed);
        setState(device);
      },
      () => live && onDone(),
    );
    return () => {
      live = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (!state) return null;

  // Shown: this opening is done whatever happens next; a Reformado's acknowledgement is stored on the server.
  const close = () => {
    markLaunchDone(REMINDER);
    if (reformed) void pushApi.acknowledgeReminder(); // a failed save just shows it again on a later opening
    onDone();
  };

  const enable = async () => {
    if (!('status' in state) || !state.status.vapidPublicKey) return;
    setBusy(true);
    setError(undefined);
    const result = await turnOn(state.status.vapidPublicKey);
    setBusy(false);
    if (result.state.kind === 'on') return close();
    setState(result.state);
    setError(result.error);
  };

  let body: ReactNode;
  let action: ReactNode = null;
  if (state.kind === 'denied') {
    body = (
      <p>
        As notificações da RTUB estão bloqueadas neste navegador. Para as receberes, permite-as nas definições do site (o
        cadeado junto ao endereço, ou as definições da app) e depois ativa-as no teu perfil.
      </p>
    );
  } else if (state.kind === 'unsupported') {
    body = (
      <p>
        Este navegador não permite notificações aqui. No iPhone e no iPad, adiciona a RTUB ao ecrã principal (Partilhar →
        Adicionar ao ecrã principal) e ativa-as a partir da app.
      </p>
    );
  } else {
    action = (
      <button type="button" className="btn btn--primary" onClick={enable} disabled={busy}>
        <Icon name="bell" />
        {busy ? 'A ativar…' : 'Ativar notificações'}
      </button>
    );
  }

  return (
    <Dialog
      title="Ativa as notificações"
      onClose={close}
      footer={
        <>
          {action}
          <button type="button" className="btn btn--ghost" onClick={close} disabled={busy}>
            Agora não
          </button>
        </>
      }
    >
      <p>A app RTUB é agora a principal fonte de informação da tuna.</p>
      <p>Recebe os avisos de atuações, ensaios e ações pendentes.</p>
      {body}
      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}
