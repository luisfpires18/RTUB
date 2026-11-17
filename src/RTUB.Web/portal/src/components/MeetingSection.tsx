// A titled, paged grid of meeting cards on /meetings, and its empty state.
import { type ReactNode } from 'react';
import { Icon, type IconName } from './icons';
import type { MeetingCard } from '../data/meetingsTypes';
import { MEETING_PAGE_SIZES, Pager } from './MeetingPager';
import { type Open } from './MeetingDialogHost';
import { MeetingCardView } from './MeetingCardView';

export function Empty({ title, message }: { title: string; message?: string }) {
  return (
    <div className="mtg-empty">
      <Icon name="calendar" className="mtg-empty__icon" />
      <p className="mtg-empty__title">{title}</p>
      {message && <p className="mtg-empty__text">{message}</p>}
    </div>
  );
}

export function MeetingSection({
  id,
  icon,
  title,
  itemLabel,
  cards,
  page,
  pageSize,
  onPage,
  onPageSize,
  empty,
  onOpen,
}: {
  id: string;
  icon: IconName;
  title: string;
  itemLabel: string;
  cards: MeetingCard[];
  page: number;
  pageSize: number;
  onPage: (p: number) => void;
  onPageSize: (s: number) => void;
  empty: ReactNode;
  onOpen: (o: Open) => void;
}) {
  const pages = Math.max(1, Math.ceil(cards.length / pageSize));
  const current = Math.min(page, pages);
  const shown = cards.slice((current - 1) * pageSize, current * pageSize);

  return (
    <section className="mtg-section" aria-labelledby={id}>
      <h2 id={id} className="mtg-section__title">
        <Icon name={icon} />
        {title}
      </h2>
      {cards.length === 0 ? (
        empty
      ) : (
        <>
          <ul className="mtg-grid">
            {shown.map((card) => (
              <li key={card.id}>
                <MeetingCardView card={card} onOpen={onOpen} />
              </li>
            ))}
          </ul>
          {cards.length > MEETING_PAGE_SIZES[0] && (
            <Pager
              total={cards.length}
              itemLabel={itemLabel}
              page={current}
              pages={pages}
              pageSize={pageSize}
              sizes={MEETING_PAGE_SIZES}
              onPage={onPage}
              onPageSize={onPageSize}
            />
          )}
        </>
      )}
    </section>
  );
}
