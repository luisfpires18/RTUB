import { useEffect, useId, useRef, type ReactNode } from 'react';
import { IconAction } from './ui/IconAction';

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
  className,
}: {
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  size?: 'md' | 'lg' | 'xl';
  /** An extra class on the <dialog> (e.g. a full-screen view on phones). */
  className?: string;
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
      className={`modal modal--${size}${className ? ` ${className}` : ''}`}
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
        <IconAction icon="close" label="Fechar" onClick={onClose} large />
      </div>
      <div className="modal__body">{children}</div>
      {footer && <div className="modal__foot">{footer}</div>}
    </dialog>
  );
}
