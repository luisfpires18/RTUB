// The /profile push-notification section.
import { useEffect, useState, type ReactNode } from 'react';
import { Loading } from './AppShared';
import { Icon, type IconName } from './icons';
import { PushPreferencesDialog } from './PushPreferences';
import { readPush, sendTest, turnOff, turnOn, type PushResult, type PushState } from '../data/push';
import { Section } from './ProfileShared';

/**
 * This browser's push notifications (task 038, push.ts). Read on load without asking anything; permission and the
 * subscription only from a button. Each device is turned on separately; the count of the member's other devices
 * comes from the server.
 */
export function PushSection() {
  const [state, setState] = useState<PushState>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [message, setMessage] = useState<string>();
  const [choosing, setChoosing] = useState(false);

  const load = () => {
    setState(undefined);
    setError(undefined);
    setMessage(undefined);
    readPush().then(setState);
  };
  useEffect(load, []);

  const run = async (action: () => Promise<PushResult & { message?: string }>) => {
    setBusy(true);
    setError(undefined);
    setMessage(undefined);
    const result = await action();
    setBusy(false);
    setState(result.state);
    setError(result.error);
    setMessage(result.message);
  };

  const key = state && 'status' in state ? state.status.vapidPublicKey : null;
  const on = () => key && run(() => turnOn(key));
  const others = state && 'others' in state ? state.others : 0;

  const button = (label: string, icon: IconName, onClick: () => unknown, primary = false) => (
    <button type="button" className={`btn ${primary ? 'btn--primary' : 'btn--ghost'} btn--sm`} onClick={onClick} disabled={busy}>
      <Icon name={icon} />
      {label}
    </button>
  );
  const offEverywhere = others > 0 && button('Desativar em todos os dispositivos', 'ban', () => run(() => turnOff(true)));

  let body: ReactNode;
  let actions: ReactNode = null;
  if (!state) body = <Loading label="A verificar as notificações…" />;
  else if (state.kind === 'unsupported')
    body = state.installed ? (
      <p>Este navegador não permite notificações push.</p>
    ) : (
      <p>
        Este navegador não permite notificações push aqui. No iPhone e no iPad, adiciona a RTUB ao ecrã principal
        (Partilhar → Adicionar ao ecrã principal) e ativa-as a partir da app.
      </p>
    );
  else if (state.kind === 'disabled') body = <p>As notificações push ainda não estão disponíveis.</p>;
  else if (state.kind === 'not-configured') body = <p>As notificações push não estão configuradas neste servidor.</p>;
  else if (state.kind === 'error' || state.kind === 'no-worker') {
    body = (
      <p>
        {state.kind === 'error'
          ? 'Não foi possível ver o estado das notificações.'
          : 'A app ainda não ficou pronta neste navegador, por isso as notificações não funcionam aqui.'}
      </p>
    );
    actions = button('Tentar outra vez', 'restore', load);
  } else if (state.kind === 'denied') {
    body = (
      <p>
        As notificações da RTUB estão bloqueadas neste navegador. Para as receberes, permite-as nas definições do site
        (o cadeado junto ao endereço, ou as definições da app) e volta aqui.
      </p>
    );
    actions = (
      <>
        {button('Verificar outra vez', 'restore', load)}
        {state.registered && button('Remover este dispositivo', 'ban', () => run(() => turnOff(false)))}
      </>
    );
  } else if (state.kind === 'off') {
    body = <p>Desligadas neste dispositivo.</p>;
    actions = (
      <>
        {button('Ativar neste dispositivo', 'bell', on, true)}
        {offEverywhere}
      </>
    );
  } else if (state.kind === 'on') {
    body = (
      <p>
        <strong>Ligadas neste dispositivo.</strong> Os avisos da tuna chegam aqui mesmo com a RTUB fechada.
      </p>
    );
    actions = (
      <>
        {button('Enviar notificação de teste', 'bell', () => run(sendTest))}
        {button('Desativar neste dispositivo', 'ban', () => run(() => turnOff(false)))}
        {offEverywhere}
      </>
    );
  } else {
    body = (
      <p>
        {state.kind === 'stale'
          ? 'A subscrição deste dispositivo já não é válida (expirou, ou a chave do servidor mudou), por isso não recebes nada aqui.'
          : 'Este navegador tem uma subscrição que a RTUB não conhece, por isso não recebes nada aqui.'}
      </p>
    );
    actions = (
      <>
        {button(state.kind === 'stale' ? 'Ativar novamente' : 'Corrigir subscrição', 'restore', on, true)}
        {button('Desativar neste dispositivo', 'ban', () => run(() => turnOff(false)))}
      </>
    );
  }

  return (
    <Section id="push" title="Notificações push" icon="bell">
      <p className="note">
        Avisos no telemóvel ou no computador, mesmo com a RTUB fechada. Ativam-se em cada dispositivo, à parte dos emails.
      </p>
      <div className="push-state" role="status" aria-live="polite">
        {body}
        {others > 0 && (
          <p className="note">
            {others === 1 ? 'Ligadas também noutro dispositivo.' : `Ligadas também noutros ${others} dispositivos.`}
          </p>
        )}
      </div>
      {actions && <div className="push-actions">{actions}</div>}
      {message && (
        <p className="note" role="status">
          {message}
        </p>
      )}
      {error && (
        <p className="form__error" role="alert">
          {error}
        </p>
      )}
      {/* Task 082 (#263): account-wide content preferences, apart from this device's switch above. */}
      <div className="push-prefs__open">
        <p className="note">O que receber vale para a tua conta, em todos os dispositivos.</p>
        <button type="button" className="btn btn--ghost btn--sm" onClick={() => setChoosing(true)}>
          <Icon name="bell" />
          Escolher o que receber
        </button>
      </div>
      {choosing && <PushPreferencesDialog onClose={() => setChoosing(false)} />}
    </Section>
  );
}

// ---------- Face ID / impressão digital (task 057) ----------
