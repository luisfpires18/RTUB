// Date wording for cards: the deadline line and Portugal-time stamps.
import type { Status } from './logisticsApi';
import { lisbonToday } from './format';

/** "Faltam 3 dias", "Falta 1 dia", "Termina hoje", "Atrasado 2 dias" against the Portuguese calendar day; nothing once done. */
export function deadline(due: string | null, status: Status): { text: string; tone: 'late' | 'soon' | 'ok' } | null {
  if (!due || status === 'Done') return null;
  const days = Math.round((Date.parse(due.slice(0, 10)) - Date.parse(lisbonToday())) / 86_400_000);
  if (days < 0) return { text: `Atrasado ${-days} ${days === -1 ? 'dia' : 'dias'}`, tone: 'late' };
  if (days === 0) return { text: 'Termina hoje', tone: 'soon' };
  return { text: days === 1 ? 'Falta 1 dia' : `Faltam ${days} dias`, tone: days <= 3 ? 'soon' : 'ok' };
}

/** "12/11/2026, 21:10" in Portugal time (the server sends UTC). */
export const at = (iso: string) =>
  new Intl.DateTimeFormat('pt-PT', { timeZone: 'Europe/Lisbon', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(
    new Date(iso),
  );
