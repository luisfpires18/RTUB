// The cancel-meeting dialog of /meetings.
import { useEffect, useId, useState } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingCancelDraft, MeetingCard, MeetingNoticeResult } from '../data/meetingsTypes';
import { MAX_REASON, problem, type Errors, fieldClass, FieldError, Banner, Spinner, MeetingSummary, PeopleList } from './MeetingUi';

export function CancelDialog({
  card,
  onClose,
  onCancelled,
}: {
  card: MeetingCard;
  onClose: () => void;
  onCancelled: (r: MeetingNoticeResult, notified: boolean) => void;
}) {
  const [draft, setDraft] = useState<MeetingCancelDraft | null>();
  const [notify, setNotify] = useState(false);
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    meetingsApi.cancelDraft(card.id).then((o) => {
      if (o.kind === 'ok') setDraft(o.data);
      else {
        setDraft(null);
        setBanner(problem(o, 'preparar o cancelamento'));
      }
    });
  }, [card.id]);

  const confirm = async () => {
    if (!reason.trim()) return setErrors({ reason: 'O motivo do cancelamento é obrigatório.' });
    setBusy(true);
    setBanner(undefined);
    setErrors({});
    const o = await meetingsApi.cancel(card.id, reason, notify);
    setBusy(false);
    if (o.kind === 'ok') onCancelled(o.data, notify);
    else if (o.kind === 'invalid') setErrors(o.errors);
    else setBanner(problem(o, 'cancelar a reunião'));
  };

  return (
    <Dialog
      title="Cancelar Reunião"
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Não Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={confirm} disabled={busy || draft === null}>
            <Spinner on={busy} />
            {busy && notify ? 'A enviar emails de cancelamento…' : 'Cancelar Reunião'}
          </button>
        </>
      }
    >
      <MeetingSummary card={card} heading="Reunião:" />
      <Banner text={banner} />
      <div className="form mtg-form">
        <label className="form__check" htmlFor={`${id}-notify`}>
          <input id={`${id}-notify`} type="checkbox" checked={notify} onChange={(e) => setNotify(e.target.checked)} />
          Notificar todos os membros por email
        </label>
        {notify &&
          (draft === undefined ? (
            <Loading label="A carregar subscritores..." />
          ) : draft ? (
            <>
              <p className="mtg-info">
                <Icon name="envelope" />
                {draft.recipients.length} / {draft.total} membros têm 'Notificações por email' ativas e irão receber esta mensagem.
              </p>
              <PeopleList title="Membros que irão receber:" people={draft.recipients} />
              <PeopleList title="Quem não irá receber:" people={draft.notReceiving} />
            </>
          ) : null)}
        <div className={fieldClass(errors, 'reason')}>
          <label htmlFor={`${id}-reason`}>
            Motivo do Cancelamento <span className="mtg-required">*</span>
          </label>
          <textarea
            id={`${id}-reason`}
            rows={4}
            maxLength={MAX_REASON}
            placeholder="Descreva o motivo do cancelamento da reunião..."
            value={reason}
            onChange={(e) => setReason(e.target.value)}
          />
          <p className="form__hint">Máximo {MAX_REASON} caracteres</p>
          <FieldError errors={errors} name="reason" />
        </div>
      </div>
    </Dialog>
  );
}
