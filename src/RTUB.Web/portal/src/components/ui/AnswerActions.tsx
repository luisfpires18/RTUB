// The button row under an inline form / confirm step: a ghost cancel button + the main action (primary or danger).
import { Spinner } from './Spinner';

export function AnswerActions({
  cancelLabel,
  onCancel,
  label,
  onConfirm,
  busy,
  disabled,
  danger,
  small,
  spinner = true,
}: {
  cancelLabel: string;
  onCancel: () => void;
  label: string;
  /** Omit for a submit button (the surrounding form handles it). */
  onConfirm?: () => void;
  busy?: boolean;
  /** Extra reason to disable the main button (it is always disabled while busy). */
  disabled?: boolean;
  danger?: boolean;
  small?: boolean;
  spinner?: boolean;
}) {
  const size = small ? ' btn--sm' : '';
  return (
    <div className="answer__actions">
      <button type="button" className={`btn btn--ghost${size}`} onClick={onCancel} disabled={busy}>
        {cancelLabel}
      </button>
      <button
        type={onConfirm ? 'button' : 'submit'}
        className={`btn ${danger ? 'btn--danger' : 'btn--primary'}${size}`}
        onClick={onConfirm}
        disabled={busy || disabled}
      >
        {spinner && busy && <Spinner small />}
        {label}
      </button>
    </div>
  );
}
