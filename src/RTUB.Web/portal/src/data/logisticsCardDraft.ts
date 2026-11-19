// The card fields being edited in the workspace, and how to tell they changed.
import type { CardDetail, Priority } from './logisticsApi';

export type Draft = { title: string; description: string; priority: Priority; startDate: string; dueDate: string; owner: { id: string; name: string; avatar: string | null } | null };

export const draftOf = (d: CardDetail): Draft => ({
  title: d.card.title,
  description: d.card.description,
  priority: d.card.priority,
  startDate: d.card.startDate?.slice(0, 10) ?? '',
  dueDate: d.card.dueDate?.slice(0, 10) ?? '',
  owner: d.assignedTo ? { id: d.assignedTo.userId ?? '', name: d.assignedTo.displayName, avatar: d.assignedTo.avatarUrl } : null,
});

export const same = (a: Draft, b: Draft) =>
  a.title === b.title && a.description === b.description && a.priority === b.priority && a.startDate === b.startDate && a.dueDate === b.dueDate &&
  (a.owner?.id ?? '') === (b.owner?.id ?? '');
