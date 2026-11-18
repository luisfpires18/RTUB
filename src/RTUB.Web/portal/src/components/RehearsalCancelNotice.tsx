// Admin/Owner dialogs to cancel a rehearsal or push a notice about it.

import { useEffect, useId, useState } from 'react';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { rehearsalsApi, type RehearsalSummary, type NoticeAudience } from '../data/rehearsalsApi';
import { problem, RehearsalLine } from './RehearsalShared';
import { Loading } from './ui/Loading';
import { FormField } from './ui/FormField';

/** Cancel (reason required): every presença is removed; no email goes out. */
export function CancelDialog({ rehearsal, onClose, onDone }: { rehearsal: RehearsalSummary; onClose: () => void; onDone: () => void }) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  const submit = async () => {
    setBusy(true);
    const o = await rehearsalsApi.cancel(rehearsal.id, reason);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'cancelar'));
    onDone();
    onClose();
  };

  return (
    <Dialog
      title="Cancelar ensaio"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            Não cancelar
          </button>
          <button type="button" className="btn btn--danger" disabled={busy} onClick={submit}>
            Cancelar ensaio
          </button>
        </>
      }
    >
      <div className="answer">
        <RehearsalLine rehearsal={rehearsal} />
        <FormField id={id} label="Motivo">
          <textarea id={id} rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
        </FormField>
        <p className="note">As presenças marcadas são removidas. Não é enviado nenhum email.</p>
        {error && <p className="form__error">{error}</p>}
      </div>
    </Dialog>
  );
}

/** A push to subscribed members about an upcoming rehearsal; counts first, then a confirm. */
export function NoticeDialog({ rehearsal, onClose }: { rehearsal: RehearsalSummary; onClose: () => void }) {
  const [audience, setAudience] = useState<NoticeAudience | null>();
  const [message, setMessage] = useState('');
  const [few, setFew] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [result, setResult] = useState<string>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const ids = { message: useId(), few: useId() };

  useEffect(() => {
    rehearsalsApi.noticeAudience(rehearsal.id).then((o) => setAudience(o.kind === 'ok' ? o.data : null));
  }, [rehearsal.id]);

  const count = audience ? (few ? audience.leitoesCaloirosSubscribed : audience.subscribed) : 0;
  const send = async () => {
    setBusy(true);
    const o = await rehearsalsApi.sendNotice(rehearsal.id, message, few);
    setBusy(false);
    setConfirming(false);
    if (o.kind !== 'ok') return setError(problem(o, 'enviar'));
    setResult(`Enviada a ${o.data.sent} ${o.data.sent === 1 ? 'membro' : 'membros'}${o.data.failed ? ` (${o.data.failed} falharam)` : ''}.`);
  };

  return (
    <Dialog title="Notificar por push" onClose={onClose}>
      <div className="answer">
        <RehearsalLine rehearsal={rehearsal} />
        {result ? (
          <p role="status">{result}</p>
        ) : audience === undefined ? (
          <Loading label="A contar quem recebe…" />
        ) : (
          <>
            <FormField id={ids.message} label="Mensagem">
              <textarea id={ids.message} rows={3} maxLength={500} value={message} onChange={(e) => (setMessage(e.target.value), setConfirming(false))} />
            </FormField>
            <label className="check" htmlFor={ids.few}>
              <input id={ids.few} type="checkbox" checked={few} onChange={(e) => (setFew(e.target.checked), setConfirming(false))} />
              Só Leitões e Caloiros
            </label>
            {audience && (
              <p className="note">
                Recebem {count} de {few ? audience.leitoesCaloirosTotal : audience.total} (quem tem as notificações ativas).
              </p>
            )}
            {error && <p className="form__error">{error}</p>}
            <div className="answer__actions">
              {confirming ? (
                <>
                  <span>Enviar a {count} membros?</span>
                  <button type="button" className="btn btn--ghost" onClick={() => setConfirming(false)}>
                    Não
                  </button>
                  <button type="button" className="btn btn--primary" disabled={busy} onClick={send}>
                    Sim, enviar
                  </button>
                </>
              ) : (
                <button type="button" className="btn btn--primary" disabled={!message.trim() || count === 0} onClick={() => setConfirming(true)}>
                  <Icon name="bell" />
                  Enviar…
                </button>
              )}
            </div>
          </>
        )}
      </div>
    </Dialog>
  );
}
