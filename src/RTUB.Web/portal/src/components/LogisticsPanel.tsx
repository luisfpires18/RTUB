// Shared frame of the card workspace panels: the titled section and the "apply" callback type.
import type { ReactNode } from 'react';
import type { Outcome } from '../data/api';
import type { CardDetail } from '../data/logisticsApi';

export const Section = ({ id, title, extra, children }: { id: string; title: string; extra?: ReactNode; children: ReactNode }) => (
  <section className="lx-detail__section" aria-labelledby={id}>
    <h3 id={id} className="lx-detail__title">
      {title} {extra}
    </h3>
    {children}
  </section>
);

export type Apply = (o: Promise<Outcome<CardDetail>>, key: string, what: string) => Promise<boolean>;
