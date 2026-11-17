// Card reminders panel.
import { useId, useState } from 'react';
import { Icon } from './icons';
import { FieldError, MemberPicker, PersonChip } from './LogisticsFields';
import { Section, type Apply } from './LogisticsPanel';
import { FREQUENCY, logisticsApi, type CardDetail, type Frequency, type MemberOption, type Reminder } from '../data/logisticsApi';
import { day, lisbonToday } from '../data/format';

/** Tomorrow at 10:00, Portugal time, as a datetime-local value. */
const tomorrowAtTen = () => {
  const d = new Date(`${lisbonToday()}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + 1);
  return `${d.toISOString().slice(0, 10)}T10:00`;
};

/** "2026-11-07T10:00:00" (Portugal time) → "07/11/2026 às 10:00". */
const when = (local: string) => `${day(local)} às ${local.slice(11, 16)}`;

type ReminderDraft = { id?: number; frequency: Frequency; at: string; people: MemberOption[]; personal: boolean };

/**
 * Lembretes (task 069). The owner and collaborators (Leitões too) set reminders for themselves; managers for any members
 * who see the board. Each one arrives as a notification (and stays in "Notificações RTUB") at the time shown, Portugal
 * time; it stops when the card is done or archived or its members lose access.
 */
export function Reminders({
  boardId,
  detail,
  apply,
  busy,
  errors,
}: {
  boardId: number;
  detail: CardDetail;
  apply: Apply;
  busy?: string;
  errors: Record<string, string>;
}) {
  const id = useId();
  const [draft, setDraft] = useState<ReminderDraft>();
  const cardId = detail.card.id;
  const reminders = detail.reminders ?? [];
  if (!detail.canRemind && reminders.length === 0) return null;

  const edit = (r: Reminder) =>
    setDraft({
      id: r.id,
      frequency: r.frequency,
      at: r.next.slice(0, 16),
      personal: r.personal,
      people: r.recipients.flatMap((p) => (p.userId ? [{ id: p.userId, displayName: p.displayName, fullName: p.fullName, avatarUrl: p.avatarUrl }] : [])),
    });
  const save = async () => {
    if (!draft) return;
    const input = {
      frequency: draft.frequency,
      nextReminderDate: `${draft.at}:00`,
      userIds: detail.canManage && !draft.personal ? draft.people.map((p) => p.id) : [],
    };
    const ok = await apply(
      draft.id ? logisticsApi.updateReminder(cardId, draft.id, input) : logisticsApi.createReminder(cardId, input),
      'reminder',
      draft.id ? 'guardar o lembrete' : 'criar o lembrete',
    );
    if (ok) setDraft(undefined);
  };
  const who = (r: Reminder) =>
    r.personal
      ? detail.canManage
        ? `Pessoal de ${r.recipients[0]?.displayName ?? 'um membro'}`
        : 'Para mim'
      : r.recipients.map((p) => p.displayName).join(', ');

  return (
    <Section id={`${id}-rem`} title="Lembretes" extra={reminders.length > 0 && <small>{reminders.length}</small>}>
      {reminders.length > 0 ? (
        <ul className="lx-reminders">
          {reminders.map((r) => (
            <li key={r.id} className="lx-reminder">
              <Icon name="bell" />
              <span className="lx-reminder__text">
                <strong>{when(r.next)}</strong>
                <small>
                  {FREQUENCY[r.frequency]} · {who(r)}
                </small>
              </span>
              {r.canEdit && draft?.id !== r.id && (
                <span className="lx-inline">
                  <button type="button" className="icon-btn icon-btn--sm" onClick={() => edit(r)} disabled={!!busy}>
                    <Icon name="pencil" />
                    <span className="sr-only">Editar o lembrete de {when(r.next)}</span>
                  </button>
                  <button
                    type="button"
                    className="icon-btn icon-btn--sm"
                    onClick={() => apply(logisticsApi.removeReminder(cardId, r.id), 'reminder', 'apagar o lembrete')}
                    disabled={!!busy}
                  >
                    <Icon name="trash" />
                    <span className="sr-only">Apagar o lembrete de {when(r.next)}</span>
                  </button>
                </span>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <p className="note">Sem lembretes.</p>
      )}

      {draft ? (
        <div className="lx-reminder-form" role="group" aria-label={draft.id ? 'Editar lembrete' : 'Novo lembrete'}>
          <div className="lx-reminder-form__when">
            <div className={errors.frequency ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor={`${id}-freq`}>Repetir</label>
              <span className="control control--select control--sm">
                <select id={`${id}-freq`} value={draft.frequency} onChange={(e) => setDraft({ ...draft, frequency: e.target.value as Frequency })}>
                  {(Object.keys(FREQUENCY) as Frequency[]).map((f) => (
                    <option key={f} value={f}>
                      {FREQUENCY[f]}
                    </option>
                  ))}
                </select>
              </span>
              <FieldError text={errors.frequency} />
            </div>
            <div className={errors.nextReminderDate ? 'form__field form__field--error' : 'form__field'}>
              <label htmlFor={`${id}-at`}>{draft.frequency === 'OneTime' ? 'Quando' : 'A partir de'}</label>
              <input id={`${id}-at`} type="datetime-local" value={draft.at} onChange={(e) => setDraft({ ...draft, at: e.target.value })} />
              <FieldError text={errors.nextReminderDate} />
            </div>
          </div>
          {detail.canManage && !draft.personal ? (
            <div className={errors.userIds ? 'form__field form__field--error' : 'form__field'}>
              <span className="form__label">Para quem</span>
              {draft.people.length > 0 ? (
                <div className="chips">
                  {draft.people.map((p) => (
                    <PersonChip
                      key={p.id}
                      name={p.displayName}
                      avatar={p.avatarUrl}
                      onRemove={() => setDraft({ ...draft, people: draft.people.filter((x) => x.id !== p.id) })}
                      removeLabel={`Remover ${p.displayName}`}
                    />
                  ))}
                </div>
              ) : (
                <p className="note">Ninguém escolhido: o lembrete fica para ti.</p>
              )}
              <MemberPicker
                label="Adicionar membro"
                boardId={boardId}
                exclude={draft.people.map((p) => p.id)}
                onPick={(m) => setDraft({ ...draft, people: [...draft.people, m] })}
              />
              <FieldError text={errors.userIds} />
            </div>
          ) : (
            <p className="note">Hora de Portugal. Chega-te como notificação e fica em Notificações RTUB.</p>
          )}
          <FieldError text={errors.reminder || errors.form} />
          <div className="lx-inline">
            <button type="button" className="btn btn--ghost btn--sm" onClick={() => setDraft(undefined)}>
              Cancelar
            </button>
            <button type="button" className="btn btn--primary btn--sm" onClick={save} disabled={!!busy || !draft.at}>
              {busy === 'reminder' && <span className="spinner spinner--small" aria-hidden="true" />}
              {draft.id ? 'Guardar lembrete' : 'Criar lembrete'}
            </button>
          </div>
        </div>
      ) : (
        <>
          {detail.canRemind && (
            <button
              type="button"
              className="btn btn--ghost btn--sm"
              onClick={() => setDraft({ frequency: 'OneTime', at: tomorrowAtTen(), people: [], personal: !detail.canManage })}
            >
              <Icon name="bell" />
              {detail.canManage ? 'Novo lembrete' : 'Lembrar-me'}
            </button>
          )}
          <FieldError text={errors.reminder} />
        </>
      )}
    </Section>
  );
}
