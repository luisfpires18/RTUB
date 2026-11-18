// Small formatting helpers shared by every area. Look here before writing a new one.

/** 1234.5 → "1 234,50 €". */
export const euros = (n: number) => `${n.toLocaleString('pt-PT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} €`;

/** "2026-10-04T00:00:00" → "04/10/2026". */
export const day = (iso: string | null) => (iso ? iso.slice(0, 10).split('-').reverse().join('/') : '');

/** Today on this device, as "yyyy-mm-dd" (the value a date input takes). */
export const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

/** Today in Portugal, as "yyyy-mm-dd", whatever the device's time zone. */
export const lisbonToday = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Lisbon' }).format(new Date());

/** 7 → "07". */
export const pad = (n: number) => String(n).padStart(2, '0');

/** Lower case without accents, for search: "Atuação" → "atuacao". */
export const fold = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
