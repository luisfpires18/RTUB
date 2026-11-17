// The push-notification dialog of /meetings.
import { useEffect, useId, useState } from 'react';
import { Loading } from './AppShared';
import { Dialog } from './Dialog';
import { Icon } from './icons';
import { meetingsApi } from '../data/meetingsApi';
import type { MeetingCard, MeetingNoticeResult, MeetingPushDraft } from '../data/meetingsTypes';
import { MAX_PUSH, problem, type Errors, fieldClass, FieldError, Banner, Spinner, MeetingSummary, PeopleList } from './MeetingUi';

export function PushDialog({ card, onClose, onSent }: { card: MeetingCard; onClose: () => void; onSent: (r: MeetingNoticeResult) => void }) {
  const [draft, setDraft] = useState<MeetingPushDraft | null>();
  const [message, setMessage] = useState('');
  const [tunoId, setTunoId] = useState('');
  const [errors, setErrors] = useState<Errors>({});
  const [banner, setBanner] = useState<string>();
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    meetingsApi.pushDraft(card.id).then((o) => {
      if (o.kind === 'ok') setDraft(o.data);
      else {
        setDraft(null);
        setBanner(problem(o, 'carregar os destinatários'));
      }
    });
  }, [card.id]);

  const send = async () => {
    if (!message.trim()) return setErrors({ message: 'A mensagem da notificação é obrigatória.' });
    setBusy(true);
    setBanner(undefined);
    setErrors({});
    const o = await meetingsApi.sendPush(card.id, message, tunoId || null);
    setBusy(false);
    if (o.kind === 'ok') onSent(o.data);
    else if (o.kind === 'invalid') {
      setErrors(o.errors);
      if (o.errors.notice) setBanner(o.errors.notice);
    } else setBanner(problem(o, 'enviar a notificação push'));
  };

  const recipients = draft?.recipients ?? [];
  const total = recipients.length + (draft?.notSubscribed.length ?? 0);

  return (
    <Dialog
      title="Enviar notificação push da reunião"
      size="lg"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--primary" onClick={send} disabled={busy || recipients.length === 0}>
            <Spinner on={busy} />
            {!busy && <Icon name="bell" />}
            Enviar Notificação Push
          </button>
        </>
      }
    >
      <MeetingSummary card={card} />
      <Banner text={banner} />
      {draft === undefined ? (
        <Loading label="A carregar destinatários..." />
      ) : draft === null ? null : (
        <div className="form mtg-form">
          {draft.tunoOptions.length > 0 && (
            <div className={fieldClass(errors, 'tunoId')}>
              <label htmlFor={`${id}-tuno`}>Adicionar um Representante de Tunos</label>
              <span className="control control--select">
                <select id={`${id}-tuno`} value={tunoId} onChange={(e) => setTunoId(e.target.value)}>
                  <option value="">Nenhum representante selecionado</option>
                  {draft.tunoOptions.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.label}
                    </option>
                  ))}
                </select>
              </span>
              <p className="form__hint">Opcionalmente adicione um representante de Tunos à lista de destinatários para reuniões CV.</p>
              <FieldError errors={errors} name="tunoId" />
            </div>
          )}
          <div className={fieldClass(errors, 'message')}>
            <label htmlFor={`${id}-message`}>
              Mensagem da Notificação <span className="mtg-required">*</span>
            </label>
            <textarea
              id={`${id}-message`}
              rows={4}
              maxLength={MAX_PUSH}
              placeholder="Ex: Reunião importante, confirma a tua presença!"
              value={message}
              onChange={(e) => setMessage(e.target.value)}
            />
            <p className="form__hint">
              A notificação terá um título automático com a data da reunião. Máximo {MAX_PUSH} caracteres ({message.length} / {MAX_PUSH}).
            </p>
            <FieldError errors={errors} name="message" />
          </div>
          <p className="mtg-info">
            <Icon name="bell" />
            {recipients.length} / {total} membros têm notificações push ativas e irão receber esta mensagem.
          </p>
          <PeopleList title="Destinatários com subscrições push ativas:" people={recipients} empty="Nenhum membro com subscrição push ativa encontrado." />
          <PeopleList title="Membros sem subscrição push:" people={draft.notSubscribed} />
        </div>
      )}
    </Dialog>
  );
}
