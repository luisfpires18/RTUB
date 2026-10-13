import type { ReactNode } from 'react';

/**
 * The one member / role / status badge of the portal (task 044 batch 3): a compact dark pill, whatever the page.
 * `tone` is only for the two genuine statuses (active / retired); a membership kind or a position is never a colour.
 * Pages that still write `<span className="member-badge">` get the same look from the same CSS.
 */
export function MemberBadge({ tone, children }: { tone?: 'active' | 'retired'; children: ReactNode }) {
  return <span className={tone ? `member-badge member-badge--${tone}` : 'member-badge'}>{children}</span>;
}
