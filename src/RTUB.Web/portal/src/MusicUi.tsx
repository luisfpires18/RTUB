import { useEffect, useId, useRef, type ReactNode } from 'react';
import { Icon, type IconName } from './icons';

/**
 * A native modal <dialog>, opened on mount: focus containment, Esc and an inert page come from the
 * platform. The parent renders it only while open and drops it in `onClose`.
 */
export function Dialog({
  title,
  onClose,
  children,
  footer,
  size = 'md',
}: {
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  size?: 'md' | 'lg';
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (dialog && !dialog.open) dialog.showModal();
    return () => dialog?.close();
  }, []);

  return (
    <dialog
      ref={ref}
      className={`modal modal--${size}`}
      aria-labelledby={titleId}
      onCancel={(e) => {
        e.preventDefault();
        onClose();
      }}
    >
      <div className="modal__head">
        <h2 id={titleId} className="modal__title">
          {title}
        </h2>
        <button type="button" className="icon-btn" onClick={onClose}>
          <Icon name="close" />
          <span className="sr-only">Fechar</span>
        </button>
      </div>
      <div className="modal__body">{children}</div>
      {footer && <div className="modal__foot">{footer}</div>}
    </dialog>
  );
}

export function ConfirmDialog({
  title,
  children,
  confirmLabel,
  busy,
  error,
  onConfirm,
  onClose,
}: {
  title: string;
  children: ReactNode;
  confirmLabel: string;
  busy: boolean;
  error?: string | null;
  onConfirm: () => void;
  onClose: () => void;
}) {
  return (
    <Dialog
      title={title}
      onClose={onClose}
      footer={
        <>
          <button type="button" className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancelar
          </button>
          <button type="button" className="btn btn--danger" onClick={onConfirm} disabled={busy}>
            {busy && <span className="spinner spinner--small" aria-hidden="true" />}
            {confirmLabel}
          </button>
        </>
      }
    >
      {children}
      {error && (
        <p className="form__banner" role="alert">
          {error}
        </p>
      )}
    </Dialog>
  );
}

export function Field({ id, label, error, hint, children }: { id: string; label: string; error?: string; hint?: string; children: ReactNode }) {
  return (
    <div className={`form__field${error ? ' form__field--error' : ''}`}>
      <label htmlFor={id}>{label}</label>
      {children}
      {hint && (
        <p id={`${id}-hint`} className="form__hint">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} className="form__error">
          {error}
        </p>
      )}
    </div>
  );
}

/** aria wiring for an input inside <Field>. */
export const fieldProps = (id: string, error?: string, hint?: string) => ({
  id,
  'aria-invalid': error ? true : undefined,
  'aria-describedby': [error ? `${id}-error` : '', hint ? `${id}-hint` : ''].filter(Boolean).join(' ') || undefined,
});

export function Badge({ icon, children }: { icon: IconName; children: ReactNode }) {
  return (
    <span className="badge">
      <Icon name={icon} />
      {children}
    </span>
  );
}

export function IconButton({
  icon,
  label,
  onClick,
  tone,
  disabled,
}: {
  icon: IconName;
  label: string;
  onClick: () => void;
  tone?: 'danger';
  disabled?: boolean;
}) {
  return (
    <button type="button" className={`icon-btn icon-btn--sm${tone ? ` icon-btn--${tone}` : ''}`} onClick={onClick} disabled={disabled} title={label}>
      <Icon name={icon} />
      <span className="sr-only">{label}</span>
    </button>
  );
}

/** Client-side paging for the statistics lists (sizes as on the retired page). */
export function Pager({
  page,
  pageSize,
  total,
  label,
  onPage,
  onPageSize,
}: {
  page: number;
  pageSize: number;
  total: number;
  label: string;
  onPage: (page: number) => void;
  onPageSize: (size: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const sizeId = useId();
  if (total <= 5) return null;
  return (
    <div className="pager">
      <button type="button" className="btn btn--ghost btn--sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        Anterior
      </button>
      <span className="pager__status">
        {page} / {pages} · {total} {label}
      </span>
      <button type="button" className="btn btn--ghost btn--sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>
        Seguinte
      </button>
      <label className="pager__size" htmlFor={sizeId}>
        Por página
        <span className="control control--select control--sm">
          <select id={sizeId} value={pageSize} onChange={(e) => onPageSize(Number(e.target.value))}>
            {[5, 10, 15, 20, 25].map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </select>
        </span>
      </label>
    </div>
  );
}

export const plays = (n: number) => `${n} ${n === 1 ? 'reprodução' : 'reproduções'}`;
