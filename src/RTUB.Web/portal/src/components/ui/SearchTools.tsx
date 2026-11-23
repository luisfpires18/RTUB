// The filter bar of a list page (the bar, a search box) and the one shared select of the portal (SelectField).
import type { ReactNode, SelectHTMLAttributes } from 'react';
import { Icon, type IconName } from '../icons';

/** `div.events-tools[role=search]`; `className` is added after "events-tools" (e.g. "mtg-tools"). */
export function ToolsBar({ className, children }: { className?: string; children: ReactNode }) {
  return (
    <div className={className ? `events-tools ${className}` : 'events-tools'} role="search">
      {children}
    </div>
  );
}

/** A search box: magnifier + `input[type=search]`. `small` is the compact `control--sm` size. */
export function SearchField({
  id,
  label,
  placeholder,
  value,
  onChange,
  small,
}: {
  id: string;
  label: string;
  placeholder: string;
  value: string;
  onChange: (value: string) => void;
  small?: boolean;
}) {
  return (
    <label className={small ? 'control control--sm' : 'control'} htmlFor={id}>
      <span className="sr-only">{label}</span>
      <Icon name="search" />
      <input id={id} type="search" placeholder={placeholder} value={value} onChange={(e) => onChange(e.target.value)} />
    </label>
  );
}

/**
 * The one select of the portal; the `<option>`s are the children. With `label`: a `label.control` with a
 * screen-reader-only text (toolbars). Without: a `span.control`, for use inside `FormField`, an outer label or with
 * `aria-label`. `small` is the compact `control--sm` size, `icon` goes before the select. Other props (`disabled`,
 * `required`, `aria-*`, the `fieldProps` spread) go to the `<select>`.
 */
export function SelectField({
  id,
  label,
  value,
  onChange,
  small,
  icon,
  children,
  ...rest
}: Omit<SelectHTMLAttributes<HTMLSelectElement>, 'value' | 'onChange' | 'id'> & {
  /** Needed with `label`; a select inside an outer `<label>` may go without. */
  id?: string;
  label?: string;
  value: string | number;
  onChange: (value: string) => void;
  small?: boolean;
  icon?: IconName;
  children: ReactNode;
}) {
  const className = small ? 'control control--select control--sm' : 'control control--select';
  const select = (
    <>
      {icon && <Icon name={icon} />}
      <select {...rest} id={id} value={value} onChange={(e) => onChange(e.target.value)}>
        {children}
      </select>
    </>
  );
  return label ? (
    <label className={className} htmlFor={id}>
      <span className="sr-only">{label}</span>
      {select}
    </label>
  ) : (
    <span className={className}>{select}</span>
  );
}
