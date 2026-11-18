// Treasury report page size and activity date label.
import { isoDay, type Activity } from './treasuryApi';
import { day } from './format';

export const PAGE = 10;

/** "04/10/2026" or "04/10/2026 - 06/10/2026": the activity badge. */
export const when = (a: Activity) =>
  a.endDate && isoDay(a.endDate) !== isoDay(a.startDate) ? `${day(a.startDate)} - ${day(a.endDate)}` : day(a.startDate);
