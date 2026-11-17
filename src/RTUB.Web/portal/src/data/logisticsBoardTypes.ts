// What the board page keeps in state: the view, the due filter, a drag, a drop and the open dialog.
import type { Card, List } from './logisticsApi';

export type View = 'board' | 'list' | 'calendar';
export type Due = '' | 'late' | 'soon' | 'none';

export type Drag = { cardId: number; fromList: number };
export type Drop = { listId: number; index: number };
export type Open =
  | { kind: 'card'; id: number }
  | { kind: 'create-card'; list: List }
  | { kind: 'list'; list?: List }
  | { kind: 'delete-list'; list: List }
  | { kind: 'delete-card'; card: Card }
  | { kind: 'move'; card: Card; listId: number }
  | { kind: 'files' };
