// A checkbox row: the box and its text in one clickable label.
import type { ReactNode } from 'react';

/** `id` (optional) ties label and box with `htmlFor`; the text is the children. */
export function CheckField({
  id,
  checked,
  onChange,
  disabled,
  children,
}: {
  id?: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  children: ReactNode;
}) {
  return (
    <label className="form__check" htmlFor={id}>
      <input id={id} type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} disabled={disabled} />
      {children}
    </label>
  );
}
