// A form field: label, the control (children), an optional hint and the error under it.
import type { ReactNode } from 'react';

/**
 * `alert`: the error is announced (role="alert"); without it the input points at it through `fieldProps`.
 * `label` may carry markup (required marker, "(Opcional)"). `heading`: the title is a `span.form__label` (no
 * `htmlFor`) for a group of controls; `hideLabel`: the label is screen-reader only.
 */
export function FormField({
  id,
  label,
  error,
  hint,
  alert,
  heading,
  hideLabel,
  children,
}: {
  id: string;
  label: ReactNode;
  error?: string;
  hint?: string;
  alert?: boolean;
  heading?: boolean;
  hideLabel?: boolean;
  children: ReactNode;
}) {
  return (
    <div className={error ? 'form__field form__field--error' : 'form__field'}>
      {heading ? (
        <span className="form__label">{label}</span>
      ) : (
        <label htmlFor={id} className={hideLabel ? 'sr-only' : undefined}>
          {label}
        </label>
      )}
      {children}
      {hint && (
        <p id={`${id}-hint`} className="form__hint">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} className="form__error" role={alert ? 'alert' : undefined}>
          {error}
        </p>
      )}
    </div>
  );
}

/** aria wiring for an input inside <FormField>. */
export const fieldProps = (id: string, error?: string, hint?: string) => ({
  id,
  'aria-invalid': error ? true : undefined,
  'aria-describedby': [error ? `${id}-error` : '', hint ? `${id}-hint` : ''].filter(Boolean).join(' ') || undefined,
});
