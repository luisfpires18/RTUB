import type { ReactNode } from 'react';

/**
 * The one member / role / status badge of the portal. `kind` picks the colours (`.member-badge--{kind}` in styles.css):
 * tuno, veterano, tunossauro, tunohonorario, fundador, caloiro, leitao, position, active, retired. Without a kind it is
 * the neutral outlined pill. The kinds are the ones the server sends (`badges[].kind`).
 */
export function MemberBadge({ kind, children }: { kind?: string; children: ReactNode }) {
  return <span className={kind ? `member-badge member-badge--${kind}` : 'member-badge'}>{children}</span>;
}

const KINDS = ['tuno', 'veterano', 'tunossauro', 'tunohonorario', 'fundador', 'caloiro', 'leitao'];

/** The kind of a category label that has no `kind` of its own ("TUNO HONORÁRIO" → tunohonorario, "LEITÃO" → leitao); Magister is a position. */
export function badgeKind(label: string) {
  const k = label
    .normalize('NFD')
    .replace(/[^a-zA-Z]/g, '')
    .toLowerCase();
  return KINDS.includes(k) ? k : k === 'magister' ? 'position' : undefined;
}
