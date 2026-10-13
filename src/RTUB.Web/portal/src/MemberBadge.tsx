import type { ReactNode } from 'react';

/**
 * The one member / role / status badge of the portal. `kind` picks the colours (`.member-badge--{kind}` in styles.css):
 * tuno, veterano, tunossauro, tunohonorario, fundador, caloiro, leitao, position, active, retired. Without a kind it is
 * the neutral outlined pill. The kinds are the ones the server sends (`badges[].kind`).
 */
export function MemberBadge({ kind, children }: { kind?: string; children: ReactNode }) {
  return <span className={kind ? `member-badge member-badge--${kind}` : 'member-badge'}>{children}</span>;
}

/** The kind of a category label that has no `kind` of its own ("TUNO HONORÁRIO" → tunohonorario, "LEITÃO" → leitao). */
export const badgeKind = (label: string) =>
  label
    .normalize('NFD')
    .replace(/[^a-zA-Z]/g, '')
    .toLowerCase();
