import { pad } from './format';

// ---------- dates: "yyyy-MM-ddTHH:mm" local text in, Portuguese text out (no Date parsing, no time zone) ----------

const WEEKDAYS = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado'];
const WEEKDAYS_LONG = ['domingo', 'segunda-feira', 'terça-feira', 'quarta-feira', 'quinta-feira', 'sexta-feira', 'sábado'];
const MONTHS = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];
const MONTHS_LONG = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];

type Parts = { y: number; m: number; d: number; hh: string; mm: string };

function parts(value: string): Parts | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2}))?/.exec(value);
  if (!match) return null;
  return { y: Number(match[1]), m: Number(match[2]), d: Number(match[3]), hh: match[4] ?? '00', mm: match[5] ?? '00' };
}

// The calendar weekday of a date (UTC arithmetic on the date alone, so the reader's zone plays no part).
const weekdayIndex = (p: Parts) => new Date(Date.UTC(p.y, p.m - 1, p.d)).getUTCDay();

/** "06 out 2026" */
export const shortDate = (value: string) => {
  const p = parts(value);
  return p ? `${pad(p.d)} ${MONTHS[p.m - 1]} ${p.y}` : value;
};

/** "Segunda" */
export const weekdayName = (value: string) => {
  const p = parts(value);
  return p ? WEEKDAYS[weekdayIndex(p)] : '';
};

/** "20:00" */
export const timeOf = (value: string) => {
  const p = parts(value);
  return p ? `${p.hh}:${p.mm}` : '';
};

/** "06/10/2026" */
export const numericDate = (value: string) => {
  const p = parts(value);
  return p ? `${pad(p.d)}/${pad(p.m)}/${p.y}` : value;
};

/** "06/10/2026 20:00" */
export const numericDateTime = (value: string) => {
  const p = parts(value);
  return p ? `${pad(p.d)}/${pad(p.m)}/${p.y} ${p.hh}:${p.mm}` : value;
};

/** "segunda-feira, 06 de outubro de 2026 • 20:00", as emails and notice dialogs show it. */
export const longDateTime = (value: string) => {
  const p = parts(value);
  return p ? `${WEEKDAYS_LONG[weekdayIndex(p)]}, ${pad(p.d)} de ${MONTHS_LONG[p.m - 1]} de ${p.y} • ${p.hh}:${p.mm}` : value;
};

/** The reader's local "now" as "yyyy-MM-ddTHH:mm", moved by whole days (for datetime-local defaults). */
export function localDateTime(addDays = 0, hour?: number): string {
  const now = new Date();
  const at = new Date(now.getFullYear(), now.getMonth(), now.getDate() + addDays, hour ?? now.getHours(), hour === undefined ? now.getMinutes() : 0);
  return `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())}T${pad(at.getHours())}:${pad(at.getMinutes())}`;
}

/** "HOJE" / "AMANHÃ" for a meeting today or tomorrow (the reader's calendar), else null. */
export function dayBadge(value: string): 'HOJE' | 'AMANHÃ' | null {
  const day = value.slice(0, 10);
  if (day === localDateTime(0).slice(0, 10)) return 'HOJE';
  if (day === localDateTime(1).slice(0, 10)) return 'AMANHÃ';
  return null;
}
