// The filter bar of a list page: the bar itself, a search box and a select, each with a screen-reader-only label.
import type { ReactNode } from 'react';
import { Icon } from '../icons';

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

/** A select; the `<option>`s are the children. `small` is the compact `control--sm` size. */
export function SelectField({
  id,
  label,
  value,
  onChange,
  small,
  children,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  small?: boolean;
  children: ReactNode;
}) {
  return (
    <label className={small ? 'control control--select control--sm' : 'control control--select'} htmlFor={id}>
      <span className="sr-only">{label}</span>
      <select id={id} value={value} onChange={(e) => onChange(e.target.value)}>
        {children}
      </select>
    </label>
  );
}
