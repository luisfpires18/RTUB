// Dialog footer: Cancelar + the submit button of the form `form`.
import { Spinner } from './Spinner';

export function DialogFooter({
  form,
  busy,
  disabled,
  onClose,
  label = 'Guardar',
}: {
  form: string;
  busy: boolean;
  disabled?: boolean;
  onClose: () => void;
  label?: string;
}) {
  return (
    <>
      <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
        Cancelar
      </button>
      <button type="submit" form={form} className="btn btn--primary" disabled={busy || disabled}>
        {busy && <Spinner small />}
        {label}
      </button>
    </>
  );
}
