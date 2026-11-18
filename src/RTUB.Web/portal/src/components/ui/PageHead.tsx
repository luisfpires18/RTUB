// The page header: eyebrow, h1 title, lead line, and optional actions (children) on the right.
import type { ReactNode } from 'react';

/** `titleClassName` is added after "page__title". */
export function PageHead({
  eyebrow,
  title,
  titleId,
  titleClassName,
  lead,
  children,
}: {
  eyebrow?: ReactNode;
  title: ReactNode;
  titleId: string;
  titleClassName?: string;
  lead?: ReactNode;
  children?: ReactNode;
}) {
  return (
    <header className="page__head events-page__head">
      <div>
        {eyebrow && <p className="eyebrow">{eyebrow}</p>}
        <h1 id={titleId} className={titleClassName ? `page__title ${titleClassName}` : 'page__title'}>
          {title}
        </h1>
        {lead && <p className="page__lead">{lead}</p>}
      </div>
      {children}
    </header>
  );
}
