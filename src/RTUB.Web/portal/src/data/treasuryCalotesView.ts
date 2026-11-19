// Treasury calotes debt type and group key.
import { type DebtGroup } from './treasuryApi';

export type Debt = DebtGroup['debts'][number];

/** Managers get member ids; everyone else sees one group (their own) or reads only, so the position is enough. */
export const key = (g: DebtGroup, i: number) => g.member.id ?? `#${i}`;
