// The page header: eyebrow, h1 title, lead line, and optional actions (children) on the right.
import type { ReactNode } from 'react';

/**
 * `titleClassName` is added after "page__title"; `className` after the header's own classes.
 * `before` (e.g. a BackLink) goes first inside the text block, `meta` (e.g. badges) between title and lead.
 * `plain`: the simple header (no "events-page__head", no inner `<div>`: eyebrow, title and lead sit straight in
 * the `<header>`, children after them).
 */
export function PageHead({
  eyebrow,
  title,
  titleId,
  titleClassName,
  lead,
  className,
  before,
  meta,
  plain,
  children,
}: {
  eyebrow?: ReactNode;
  title: ReactNode;
  titleId: string;
  titleClassName?: string;
  lead?: ReactNode;
  className?: string;
  before?: ReactNode;
  meta?: ReactNode;
  plain?: boolean;
  children?: ReactNode;
}) {
  const text = (
    <>
      {before}
      {eyebrow && <p className="eyebrow">{eyebrow}</p>}
      <h1 id={titleId} className={titleClassName ? `page__title ${titleClassName}` : 'page__title'}>
        {title}
      </h1>
      {meta}
      {lead && <p className="page__lead">{lead}</p>}
    </>
  );
  const classes = ['page__head', plain ? '' : 'events-page__head', className ?? ''].filter(Boolean).join(' ');
  return (
    <header className={classes}>
      {plain ? text : <div>{text}</div>}
      {children}
    </header>
  );
}
