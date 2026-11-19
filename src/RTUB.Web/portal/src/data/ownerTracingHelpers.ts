// Owner tracing constants and pure display helpers.
import { entityLabel, type AuditEntry } from './ownerApi';
import { pad } from './format';

export const CONFIRMATION = 'DELETE ALL';

/** The card's identity: the stored name, today's name when the stored one is weak, else the type and the id. */
export const primaryName = (e: AuditEntry) =>
  e.currentDisplayName || e.entityDisplayName || (e.entityId !== null ? `${entityLabel(e.entityType)} #${e.entityId}` : entityLabel(e.entityType));

/** dd/MM/yyyy HH:mm in the reader's time zone. */
export const stamp = (iso: string) => {
  const d = new Date(iso);
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

export const actionTone = (action: string) =>
  action === 'Created' ? 'ok' : action === 'Modified' ? 'warn' : action === 'Deleted' || action === 'Role Removed' ? 'danger' : 'info';

/** Indented JSON when the changes are JSON, the raw text otherwise. */
export const pretty = (changes: string | null) => {
  if (!changes) return 'Sem alterações registadas.';
  try {
    return JSON.stringify(JSON.parse(changes), null, 2);
  } catch {
    return changes;
  }
};
