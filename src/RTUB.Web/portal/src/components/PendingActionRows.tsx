// Pending actions answer dialog and row.
import { useId, useState, type FormEvent } from 'react';
import { Dialog } from './Dialog';
import { type ActionButton, type ActionRow, type Answer, type InstrumentOption } from '../data/pendingActionsApi';
import { NOTES_MAX, rowDate } from '../data/pendingActionsView';
import { FieldError } from './ui/FieldError';
import { AnswerActions } from './ui/AnswerActions';
import { FormField } from './ui/FormField';
import { SelectField } from './ui/SearchTools';

/** Vou / Fui with a choice of instrument (and an optional note), as on the module pages. Cancelling sends nothing. */
export function AnswerDialog({
  row,
  label,
  instruments,
  defaultInstrument,
  onClose,
  onSubmit,
}: {
  row: ActionRow;
  label: string;
  instruments: InstrumentOption[];
  defaultInstrument: string | null;
  onClose: () => void;
  onSubmit: (a: Answer) => Promise<string | null>;
}) {
  const [instrument, setInstrument] = useState(defaultInstrument ?? '');
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const ids = { instrument: useId(), notes: useId() };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    const failed = await onSubmit({ answer: 'yes', instrument: instrument || null, notes: notes.trim() || null });
    setBusy(false);
    setError(failed);
  };

  return (
    <Dialog title={`${label}: ${row.title}`} onClose={onClose}>
      <form className="answer" onSubmit={submit}>
        <FormField id={ids.instrument} label="Instrumento">
          <SelectField id={ids.instrument} value={instrument} onChange={setInstrument}>
            <option value="">Sem instrumento</option>
            {instruments.map((i) => (
              <option key={i.value} value={i.value}>
                {i.label}
              </option>
            ))}
          </SelectField>
        </FormField>
        <FormField id={ids.notes} label="Nota (opcional)">
          <textarea id={ids.notes} rows={2} maxLength={NOTES_MAX[row.kind] ?? 500} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </FormField>
        <FieldError text={error ?? undefined} />
        <AnswerActions cancelLabel="Cancelar" onCancel={onClose} label="Confirmar" busy={busy} />
      </form>
    </Dialog>
  );
}

export function Row({
  row,
  today,
  busy,
  onDismiss,
  onAnswer,
}: {
  row: ActionRow;
  today: string;
  busy: boolean;
  onDismiss: (row: ActionRow) => void;
  onAnswer: (row: ActionRow, button: ActionButton) => void;
}) {
  return (
    <li className="pa-row">
      <div className="pa-row__text">
        <strong className="pa-row__title">
          {row.href ? <a href={row.href}>{row.title}</a> : row.title}
          {row.required && <span className="pa-required">Obrigatório</span>}
        </strong>
        {(row.date || row.context) && (
          <span className="pa-row__meta">
            {row.date && (
              <time dateTime={row.date}>
                {rowDate(row.date, today)}
                {row.time && ` · ${row.time}`}
              </time>
            )}
            {row.context && <span>{row.context}</span>}
          </span>
        )}
      </div>
      <div className="pa-row__actions">
        {row.actions.map((a) =>
          a.answer ? (
            <button
              key={a.answer}
              type="button"
              className={`btn btn--sm ${a.primary ? 'btn--primary' : 'btn--ghost'}`}
              disabled={busy}
              aria-label={`${a.label}: ${row.title}`}
              onClick={() => onAnswer(row, a)}
            >
              {a.label}
            </button>
          ) : (
            <a key={a.href} className={`btn btn--sm ${a.primary ? 'btn--primary' : 'btn--ghost'}`} href={a.href!}>
              {a.label}
            </a>
          ),
        )}
        {row.dismissible && !row.required && (
          <button type="button" className="btn btn--sm btn--ghost" disabled={busy} onClick={() => onDismiss(row)}>
            {row.dismissLabel ?? 'Agora não'}
          </button>
        )}
      </div>
    </li>
  );
}
