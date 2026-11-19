// Section heading and "see more" link shared by the home page sections.
import { type ReactNode } from 'react';
import { Icon } from './icons';

export function SectionHead({
  id,
  eyebrow,
  title,
  note,
  children,
}: {
  id: string;
  eyebrow: string;
  title: string;
  note?: string;
  children?: ReactNode;
}) {
  return (
    <header className="section__head">
      <p className="eyebrow">{eyebrow}</p>
      <h2 id={id} className="section__title">
        {title}
      </h2>
      {children && <p className="section__lead">{children}</p>}
      {note && <p className="note">{note}</p>}
    </header>
  );
}

export function MoreLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a className="more" href={href}>
      {children}
      <Icon name="arrow" />
    </a>
  );
}
