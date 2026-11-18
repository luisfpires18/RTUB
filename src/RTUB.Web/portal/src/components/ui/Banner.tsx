// A form-level message line (red banner), announced by default; renders nothing without text or children.
import type { ReactNode } from 'react';

/**
 * `children` go after the text (e.g. a retry button). `role`: "alert" (default), "status", or "none" for a
 * banner that is not announced. `block`: a `<div>` instead of a `<p>` (needed when the content has blocks).
 * `className` is added after "form__banner".
 */
export function Banner({
  text,
  children,
  role = 'alert',
  block,
  className,
}: {
  text?: string | null;
  children?: ReactNode;
  role?: 'alert' | 'status' | 'none';
  block?: boolean;
  className?: string;
}) {
  if (!text && !children) return null;
  const Tag = block ? 'div' : 'p';
  return (
    <Tag className={className ? `form__banner ${className}` : 'form__banner'} role={role === 'none' ? undefined : role}>
      {text}
      {children}
    </Tag>
  );
}
