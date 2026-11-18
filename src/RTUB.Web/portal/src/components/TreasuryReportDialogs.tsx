// Treasury report balance and activity dialogs.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { isoDay, problem, treasuryApi, type Activity, type ReportEvent, type Report } from '../data/treasuryApi';
import { day, today } from '../data/format';
import { FormField } from './ui/FormField';
import { DialogFooter } from './ui/DialogFooter';
import { Banner } from './ui/Banner';

/** "Editar Dinheiro no Banco / em Caixa": one value, any sign; 0 clears it. */
export function BalanceDialog({ report, kind, onClose, onSaved }: { report: Report; kind: 'bank' | 'cash'; onClose: () => void; onSaved: (r: Report) => void }) {
  const [value, setValue] = useState(String(kind === 'bank' ? report.totals.bank : report.totals.cash));
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const n = Number(value.replace(',', '.'));
    if (value.trim() === '' || Number.isNaN(n)) return setError('Indique um valor em euros.');
    setBusy(true);
    const o = await treasuryApi.setBalance(report.id, kind, n);
    setBusy(false);
    if (o.kind !== 'ok') return setError(problem(o, 'guardar o valor'));
    onSaved(o.data);
    onClose();
  };

  return (
    <Dialog
      title={kind === 'bank' ? 'Editar dinheiro no banco' : 'Editar dinheiro em caixa'}
      onClose={onClose}
      footer={<DialogFooter form={`${fid}-form`} busy={busy} onClose={onClose} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <FormField alert id={`${fid}-value`} label="Valor (€)" error={error}>
          <input id={`${fid}-value`} type="number" inputMode="decimal" step="0.01" value={value} onChange={(e) => setValue(e.target.value)} />
        </FormField>
      </form>
    </Dialog>
  );
}

/**
 * "Nova atividade" / "Editar atividade": name, one date or a range, description, and the event it accounts
 * for. Opened for an event (from "As minhas ações pendentes"), it starts with the event's name and dates.
 */
export function ActivityDialog({
  reportId,
  activity,
  event,
  events,
  onClose,
  onSaved,
}: {
  reportId: number;
  activity?: Activity;
  event?: ReportEvent;
  events: ReportEvent[];
  onClose: () => void;
  onSaved: (r: Report) => void;
}) {
  const [name, setName] = useState(activity?.name ?? event?.name ?? '');
  const [range, setRange] = useState(!!(activity ? activity.endDate : event?.endDate));
  const [start, setStart] = useState(isoDay(activity?.startDate ?? event?.date ?? null) || today());
  const [end, setEnd] = useState(isoDay(activity ? activity.endDate : (event?.endDate ?? null)));
  const [eventId, setEventId] = useState<number | null>(activity ? activity.eventId : (event?.id ?? null));
  const [description, setDescription] = useState(activity?.description ?? '');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const fid = useId();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    const input = { name, startDate: start, endDate: range && end ? end : null, description, eventId };
    const o = activity ? await treasuryApi.updateActivity(activity.id, input) : await treasuryApi.createActivity(reportId, input);
    setBusy(false);
    if (o.kind === 'invalid') return setErrors(o.errors);
    if (o.kind !== 'ok') return setErrors({ form: problem(o, 'guardar a atividade') });
    onSaved(o.data);
    onClose();
  };

  return (
    <Dialog
      title={activity ? 'Editar atividade' : 'Nova atividade'}
      onClose={onClose}
      footer={<DialogFooter form={`${fid}-form`} busy={busy} disabled={!name.trim() || !start} onClose={onClose} />}
    >
      <form id={`${fid}-form`} className="form" onSubmit={submit} noValidate>
        <Banner text={errors.form} />
        <FormField alert id={`${fid}-name`} label="Nome" error={errors.name}>
          <input id={`${fid}-name`} type="text" maxLength={200} value={name} onChange={(e) => setName(e.target.value)} />
        </FormField>
        <label className="check">
          <input type="checkbox" checked={range} onChange={(e) => setRange(e.target.checked)} />
          Intervalo de datas (em vez de data única)
        </label>
        <div className="form__row">
          <FormField alert id={`${fid}-start`} label={range ? 'Data de início' : 'Data'} error={errors.startDate}>
            <input id={`${fid}-start`} type="date" value={start} onChange={(e) => setStart(e.target.value)} />
          </FormField>
          {range && (
            <FormField alert id={`${fid}-end`} label="Data de fim (opcional)" error={errors.endDate}>
              <input id={`${fid}-end`} type="date" min={start} value={end} onChange={(e) => setEnd(e.target.value)} />
            </FormField>
          )}
        </div>
        {events.length > 0 && (
          <FormField alert id={`${fid}-event`} label="Atuação (opcional)" error={errors.eventId}>
            <span className="control control--select">
              <select id={`${fid}-event`} value={eventId ?? ''} onChange={(e) => setEventId(Number(e.target.value) || null)}>
                <option value="">Nenhuma</option>
                {events.map((ev) => (
                  <option key={ev.id} value={ev.id}>
                    {ev.name} · {day(ev.date)}
                  </option>
                ))}
              </select>
            </span>
          </FormField>
        )}
        <FormField alert id={`${fid}-desc`} label="Descrição (opcional)" error={errors.description}>
          <textarea id={`${fid}-desc`} rows={3} maxLength={1000} value={description} onChange={(e) => setDescription(e.target.value)} />
        </FormField>
      </form>
    </Dialog>
  );
}
