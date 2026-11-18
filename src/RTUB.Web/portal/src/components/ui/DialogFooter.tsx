// Dialog footers: Cancelar + the main action (DialogFooter), or one ghost close button (CloseFooter).
import type { ReactNode } from 'react';
import { Icon, type IconName } from '../icons';
import { Spinner } from './Spinner';

/**
 * Main action is a form submit (`form`) unless `onSubmit` is given (then a plain button that calls it).
 * `icon` shows beside the label when not busy; `extra` sits between Cancelar and the main action.
 */
export function DialogFooter({
  form,
  busy,
  disabled,
  onClose,
  onSubmit,
  label = 'Guardar',
  cancelLabel = 'Cancelar',
  icon,
  danger,
  extra,
}: {
  form?: string;
  busy: boolean;
  disabled?: boolean;
  onClose: () => void;
  onSubmit?: () => void;
  label?: string;
  cancelLabel?: string;
  icon?: IconName;
  danger?: boolean;
  extra?: ReactNode;
}) {
  return (
    <>
      <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
        {cancelLabel}
      </button>
      {extra}
      <button
        type={onSubmit ? 'button' : 'submit'}
        form={onSubmit ? undefined : form}
        className={danger ? 'btn btn--danger' : 'btn btn--primary'}
        onClick={onSubmit}
        disabled={busy || disabled}
      >
        {busy && <Spinner small />}
        {!busy && icon && <Icon name={icon} />}
        {label}
      </button>
    </>
  );
}

/** A footer with one ghost button ("Fechar", "Voltar"). */
export function CloseFooter({ onClose, label = 'Fechar' }: { onClose: () => void; label?: string }) {
  return (
    <button type="button" className="btn btn--ghost" onClick={onClose}>
      {label}
    </button>
  );
}
