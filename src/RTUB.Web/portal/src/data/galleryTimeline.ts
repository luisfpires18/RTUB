// Gallery date wording and the year/month grouping of the photo list.
import { type GalleryItem } from './api';


const monthName = (month: number) =>
  new Intl.DateTimeFormat('pt-PT', { month: 'long' }).format(new Date(2000, month - 1, 1));

/** "12 de março de 2025", "março de 2025" or "2025": only the precision the item has. */
export function dateLabel(item: Pick<GalleryItem, 'year' | 'month' | 'day'>) {
  if (!item.month) return String(item.year);
  const month = `${monthName(item.month)} de ${item.year}`;
  return item.day ? `${item.day} de ${month}` : month;
}

export type MonthGroup = { key: string; label: string; items: GalleryItem[] };
export type YearGroup = { year: number; months: MonthGroup[] };

/** The server already sends newest first; this only cuts the list into years and months. */
export function groupTimeline(items: GalleryItem[]): YearGroup[] {
  const years: YearGroup[] = [];
  for (const item of items) {
    let year = years[years.length - 1];
    if (year?.year !== item.year) years.push((year = { year: item.year, months: [] }));
    const key = String(item.month ?? 0);
    let month = year.months[year.months.length - 1];
    if (month?.key !== key) {
      year.months.push(
        (month = { key, label: item.month ? monthName(item.month) : 'Sem mês indicado', items: [] }),
      );
    }
    month.items.push(item);
  }
  return years;
}
