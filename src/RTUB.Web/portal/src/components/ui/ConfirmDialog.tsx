// The yes / no dialog: Cancelar + a danger (or primary) action, the question as children, the error under it.
import { useState, type ReactNode } from 'react';
import { Dialog } from '../Dialog';
import { Icon, type IconName } from '../icons';
import { Banner } from './Banner';
import { Spinner } from './Spinner';

type Common = {
  title: string;
  children: ReactNode;
  confirmLabel: string;
  cancelLabel?: string;
  confirmIcon?: IconName;
  onClose: () => void;
};

/** The caller holds `busy` and `error`. `danger` defaults to true. */
export function ConfirmDialog({
  title,
  children,
  confirmLabel,
  cancelLabel = 'Cancelar',
  confirmIcon,
  danger = true,
  busy,
  error,
  onConfirm,
  onClose,
}: Common & { danger?: boolean; busy: boolean; error?: string | null; onConfirm: () => void }) {
  return (
    <Dialog
      title={title}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            {cancelLabel}
          </button>
          <button type="button" className={danger ? 'btn btn--danger' : 'btn btn--primary'} onClick={onConfirm} disabled={busy}>
            {busy && <Spinner small />}
            {!busy && confirmIcon && <Icon name={confirmIcon} />}
            {confirmLabel}
          </button>
        </>
      }
    >
      {children}
      <Banner text={error} />
    </Dialog>
  );
}

/**
 * ConfirmDialog that holds its own busy state: `onConfirm` answers an error sentence (shown, the dialog stays)
 * or nothing (the dialog closes). `danger` defaults to false.
 */
export function AsyncConfirmDialog({ danger = false, onConfirm, ...rest }: Common & { danger?: boolean; onConfirm: () => Promise<string | void> }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const confirm = async () => {
    setBusy(true);
    setError(undefined);
    const failure = await onConfirm();
    setBusy(false);
    if (failure) setError(failure);
    else rest.onClose();
  };
  return <ConfirmDialog {...rest} danger={danger} busy={busy} error={error} onConfirm={confirm} />;
}
