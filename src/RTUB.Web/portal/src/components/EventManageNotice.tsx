// Dialog to send an email / push notice about an event.

import { useId, useState } from 'react';
import { Dialog } from './Dialog';
import { eventsApi, type EventSummary } from '../data/eventsApi';
import { Icon } from './icons';
import { failure, EventLine, members, useAudience } from './EventManageShared';
import { Segmented } from './ui/Segmented';
import { FieldError } from './ui/FieldError';
import { AnswerActions } from './ui/AnswerActions';

/**
 * One notice about an upcoming event: an email ("Nova atuação" or
 * "Lembrete") to the members with email notifications on, or a push message to the members with
 * push on (optionally Leitões and Caloiros only). The audience is shown before anything is sent,
 * and sending takes a second, explicit confirmation.
 */
export function NoticeDialog({ event, onClose }: { event: EventSummary; onClose: () => void }) {
  const [channel, setChannel] = useState<'email' | 'push'>('email');
  const [kind, setKind] = useState<'new' | 'reminder'>('new');
  const [message, setMessage] = useState('');
  const [young, setYoung] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [sent, setSent] = useState<{ count: number; warning: string | null }>();
  const audience = useAudience(event.id);
  const ids = { kind: useId(), message: useId() };

  const reach = !audience
    ? null
    : channel === 'email'
      ? { to: audience.emailSubscribed, of: audience.emailTotal, what: 'notificações por email' }
      : young
        ? { to: audience.pushLeitoesCaloirosSubscribed, of: audience.pushLeitoesCaloirosTotal, what: 'notificações push (Leitões e Caloiros)' }
        : { to: audience.pushSubscribed, of: audience.pushTotal, what: 'notificações push' };
  const ready = !!reach && reach.to > 0 && (channel === 'email' || message.trim().length > 0);

  // Any change goes back to the first step: what is confirmed is exactly what is sent.
  const change = (f: () => void) => {
    f();
    setConfirming(false);
    setError(undefined);
  };

  const send = async () => {
    setBusy(true);
    const o = await eventsApi.sendNotice(
      event.id,
      channel === 'email'
        ? { channel, kind, message: null, onlyLeitoesAndCaloiros: false }
        : { channel, kind: null, message: message.trim(), onlyLeitoesAndCaloiros: young },
    );
    setBusy(false);
    setConfirming(false);
    if (o.kind === 'ok') return setSent({ count: o.data.sent, warning: o.data.warning });
    setError(o.kind === 'invalid' ? Object.values(o.errors)[0] : failure(o, 'enviar'));
  };

  return (
    <Dialog title="Enviar aviso" onClose={onClose}>
      <div className="answer">
        <EventLine event={event} />
        {sent ? (
          <>
            <p role="status">{channel === 'email' ? `Email enviado a ${members(sent.count)}.` : `Notificação enviada a ${members(sent.count)}.`}</p>
            {sent.warning && (
              <p className="warning">
                <Icon name="warning" />
                {sent.warning}
              </p>
            )}
            <div className="answer__actions">
              <button type="button" className="btn btn--primary" onClick={onClose}>
                Fechar
              </button>
            </div>
          </>
        ) : (
          <>
            <Segmented label="Canal" role="radiogroup">
              {(['email', 'push'] as const).map((c) => (
                <label key={c} className={channel === c ? 'segmented__option is-on' : 'segmented__option'}>
                  <input type="radio" name="notice-channel" checked={channel === c} onChange={() => change(() => setChannel(c))} disabled={busy} />
                  <Icon name={c === 'email' ? 'envelope' : 'bell'} />
                  {c === 'email' ? 'Email' : 'Push'}
                </label>
              ))}
            </Segmented>

            {channel === 'email' ? (
              <div className="form__field">
                <label htmlFor={ids.kind}>Tipo de email</label>
                <span className="control control--select">
                  <select id={ids.kind} value={kind} onChange={(e) => change(() => setKind(e.target.value as 'new' | 'reminder'))} disabled={busy}>
                    <option value="new">Nova atuação</option>
                    <option value="reminder">Lembrete de atuação</option>
                  </select>
                </span>
                <p className="form__hint">
                  {kind === 'new' ? 'Apresenta a atuação aos membros.' : 'Lembra os membros da atuação que se aproxima.'}
                </p>
              </div>
            ) : (
              <>
                <div className="form__field">
                  <label htmlFor={ids.message}>Mensagem</label>
                  <textarea
                    id={ids.message}
                    rows={3}
                    maxLength={500}
                    value={message}
                    onChange={(e) => change(() => setMessage(e.target.value))}
                    disabled={busy}
                  />
                  <p className="form__hint">O título é o nome da atuação. Máximo 500 caracteres ({message.length}/500).</p>
                </div>
                <label className="form__check">
                  <input type="checkbox" checked={young} onChange={(e) => change(() => setYoung(e.target.checked))} disabled={busy} />
                  Só Leitões e Caloiros
                </label>
              </>
            )}

            <p className="notice-audience" role="status">
              <Icon name="person" />
              {audience === undefined
                ? 'A contar os destinatários…'
                : reach
                  ? `${members(reach.to)} de ${reach.of} com ${reach.what} ativas.`
                  : 'Não foi possível contar os destinatários.'}
            </p>

            <FieldError text={error} />

            {confirming ? (
              <div className="notice-confirm" role="alert">
                <p>
                  <strong>
                    Enviar {channel === 'email' ? 'este email' : 'esta notificação'} a {members(reach?.to ?? 0)}?
                  </strong>{' '}
                  Não dá para desfazer.
                </p>
                <AnswerActions cancelLabel="Voltar" onCancel={() => setConfirming(false)} label="Sim, enviar" onConfirm={send} busy={busy} />
              </div>
            ) : (
              <div className="answer__actions">
                <button type="button" className="btn btn--ghost" onClick={onClose}>
                  Cancelar
                </button>
                <button type="button" className="btn btn--primary" onClick={() => setConfirming(true)} disabled={!ready}>
                  <Icon name="send" />
                  Enviar…
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </Dialog>
  );
}
