import { useEffect, useId, useRef, type ReactNode } from 'react';
import { Icon } from './icons';

/**
 * A native modal <dialog>, opened on mount: focus containment, Esc and an inert page come from the
 * platform. The parent renders it only while open and drops it in `onClose`. Shared by every
 * portal module (Music, Events).
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
