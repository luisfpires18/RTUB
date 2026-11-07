// Task 079: self-check of the Google Calendar link builder (portal/src/calendar.ts). Run: npm run check:calendar
// The builder only uses Date.UTC / getUTC*, so the device's time zone never matters; Google applies ctz=Europe/Lisbon.
import assert from 'node:assert/strict';
import { build } from 'esbuild';

const out = await build({ entryPoints: [new URL('../src/calendar.ts', import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1')], bundle: false, write: false, format: 'esm' });
const { calendarDates, googleCalendarUrl } = await import(`data:text/javascript,${encodeURIComponent(out.outputFiles[0].text)}`);
const base = { title: 'RTUB · Serenata & Festa #1', location: 'Sé de Bragança', path: '/events/12', end: null, endDate: null };
// winter timed, suggested 2h
assert.deepEqual(calendarDates({ ...base, date: '2026-12-12', start: '21:30' }, 120), { dates: '20261212T213000/20261212T233000', allDay: false, suggested: true });
// summer: wall clock kept, Google applies ctz (no browser zone)
assert.equal(calendarDates({ ...base, date: '2026-07-01', start: '21:00' }, 60).dates, '20260701T210000/20260701T220000');
// suggested crossing midnight + month rollover
assert.equal(calendarDates({ ...base, date: '2026-10-31', start: '23:00' }, 120).dates, '20261031T230000/20261101T010000');
// rehearsal 21:00-00:00 → next day
assert.deepEqual(calendarDates({ ...base, date: '2026-12-31', start: '21:00', end: '00:00' }, 120), { dates: '20261231T210000/20270101T000000', allDay: false, suggested: false });
// date-only → all-day, end exclusive
assert.deepEqual(calendarDates({ ...base, date: '2026-11-08', start: null }, 120), { dates: '20261108/20261109', allDay: true, suggested: false });
// multi-day → all-day through last day
assert.equal(calendarDates({ ...base, date: '2027-02-27', start: '10:00', endDate: '2027-03-01' }, 120).dates, '20270227/20270302');
// URL: fixed destination, encoding, ctz, no notes
const url = new URL(googleCalendarUrl({ ...base, date: '2026-12-12', start: '21:30' }, 120, 'https://rtub.pt'));
assert.equal(url.origin + url.pathname, 'https://calendar.google.com/calendar/render');
assert.equal(url.searchParams.get('text'), 'RTUB · Serenata & Festa #1');
assert.equal(url.searchParams.get('ctz'), 'Europe/Lisbon');
assert.equal(url.searchParams.get('location'), 'Sé de Bragança');
assert.match(url.searchParams.get('details'), /^RTUB: https:\/\/rtub\.pt\/events\/12\n/);
assert.equal(url.searchParams.getAll('text').length, 1);
console.log('calendar link builder: ok');
