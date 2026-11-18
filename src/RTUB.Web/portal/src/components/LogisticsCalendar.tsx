// The board calendar view: a month grid on wide screens, an agenda on phones.
import { useState } from 'react';
import { Icon } from './icons';
import { STATUS, type Card } from '../data/logisticsApi';
import { day, lisbonToday } from '../data/format';
import { dueDay, isLate, type Row } from '../data/logisticsRows';
import { IconAction } from './ui/IconAction';

const MONTHS = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];
const WEEKDAYS = ['Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb', 'Dom'];
const iso = (d: Date) => d.toISOString().slice(0, 10);
const shiftMonth = (month: string, by: number) => {
  const d = new Date(`${month}-01T00:00:00Z`);
  d.setUTCMonth(d.getUTCMonth() + by);
  return iso(d).slice(0, 7);
};
const longDay = (date: string) => {
  const d = new Date(`${date}T00:00:00Z`);
  return `${['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado'][d.getUTCDay()]}, ${d.getUTCDate()} de ${MONTHS[d.getUTCMonth()]}`;
};

function Chip({ card, onOpen, today }: { card: Card; onOpen: (id: number) => void; today: string }) {
  const late = isLate(card, today);
  return (
    <button
      type="button"
      className={`lx-cal__chip lx-cal__chip--${card.status.toLowerCase()}${late ? ' lx-cal__chip--late' : ''}`}
      onClick={() => onOpen(card.id)}
      title={`${card.title} · ${STATUS[card.status].name}${late ? ' · atrasado' : ''}`}
    >
      <span className="sr-only">{STATUS[card.status].name}{late ? ', atrasado' : ''}: </span>
      {card.title}
    </button>
  );
}

/**
 * Calendário: a month grid (Monday first, Portugal calendar days) on wider screens and an agenda on phones; overdue
 * cards are marked; cards without a deadline are never dropped - they sit in "Sem prazo" below.
 */
export function CalendarView({ rows, month, onMonth, onOpen }: { rows: Row[]; month: string; onMonth: (m: string) => void; onOpen: (id: number) => void }) {
  const today = lisbonToday();
  const [showDone, setShowDone] = useState(true);
  const cards = rows.map((r) => r.card).filter((c) => showDone || c.status !== 'Done');
  const byDay = new Map<string, Card[]>();
  for (const c of cards) {
    const d = dueDay(c);
    if (d) byDay.set(d, [...(byDay.get(d) ?? []), c]);
  }
  const undated = cards.filter((c) => !dueDay(c));
  const [y, m] = month.split('-').map(Number);
  const first = new Date(Date.UTC(y, m - 1, 1));
  const start = new Date(first);
  start.setUTCDate(1 - ((first.getUTCDay() + 6) % 7));
  const days = Array.from({ length: 42 }, (_, i) => {
    const d = new Date(start);
    d.setUTCDate(start.getUTCDate() + i);
    return iso(d);
  });
  const weeks = Array.from({ length: days[35].slice(0, 7) === month ? 6 : 5 }, (_, w) => days.slice(w * 7, w * 7 + 7));
  const inMonth = [...byDay.keys()].filter((d) => d.startsWith(month)).sort();
  const lateBefore = cards.filter((c) => isLate(c, today) && dueDay(c)! < `${month}-01`);
  const label = `${MONTHS[m - 1]} de ${y}`;

  return (
    <section className="lx-cal" aria-label={`Calendário, ${label}`}>
      <div className="lx-cal__bar">
        <IconAction icon="chevronLeft" label="Mês anterior" onClick={() => onMonth(shiftMonth(month, -1))} />
        <h2 className="lx-cal__title" aria-live="polite">
          {label}
        </h2>
        <IconAction icon="chevronRight" label="Mês seguinte" onClick={() => onMonth(shiftMonth(month, 1))} />
        <button type="button" className="btn btn--ghost btn--sm" onClick={() => onMonth(today.slice(0, 7))} disabled={month === today.slice(0, 7)}>
          Hoje
        </button>
        <label className="lx-cal__done">
          <input type="checkbox" checked={showDone} onChange={(e) => setShowDone(e.target.checked)} />
          Mostrar feitos
        </label>
      </div>

      {lateBefore.length > 0 && (
        <p className="lx-cal__late" role="status">
          <Icon name="warning" />
          <span>
            {lateBefore.length} {lateBefore.length === 1 ? 'cartão atrasado' : 'cartões atrasados'} de meses anteriores:{' '}
            {lateBefore.map((c, i) => (
              <span key={c.id}>
                {i > 0 && ', '}
                <button type="button" className="lx-link" onClick={() => onOpen(c.id)}>
                  {c.title}
                </button>{' '}
                ({day(c.dueDate)})
              </span>
            ))}
          </span>
        </p>
      )}

      <table className="lx-cal__grid">
        <thead>
          <tr>
            {WEEKDAYS.map((w) => (
              <th key={w} scope="col">
                {w}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {weeks.map((week) => (
            <tr key={week[0]}>
              {week.map((d) => (
                <td
                  key={d}
                  className={`lx-cal__day${d.slice(0, 7) !== month ? ' lx-cal__day--other' : ''}${d === today ? ' lx-cal__day--today' : ''}`}
                  aria-label={longDay(d)}
                >
                  <span className="lx-cal__num">{Number(d.slice(8))}</span>
                  {(byDay.get(d) ?? []).map((c) => (
                    <Chip key={c.id} card={c} onOpen={onOpen} today={today} />
                  ))}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>

      <ol className="lx-agenda" aria-label={`Agenda de ${label}`}>
        {inMonth.length === 0 && <li className="lx-muted">Nada com prazo em {label}.</li>}
        {inMonth.map((d) => (
          <li key={d} className={d === today ? 'lx-agenda__day lx-agenda__day--today' : 'lx-agenda__day'}>
            <h3>
              {longDay(d)}
              {d === today && <span className="lx-chip">Hoje</span>}
            </h3>
            {byDay.get(d)!.map((c) => (
              <Chip key={c.id} card={c} onOpen={onOpen} today={today} />
            ))}
          </li>
        ))}
      </ol>

      <details className="lx-cal__undated" open={undated.length > 0 && undated.length <= 12}>
        <summary>
          Sem prazo <small>{undated.length}</small>
        </summary>
        {undated.length === 0 ? (
          <p className="lx-muted">Todos os cartões mostrados têm prazo.</p>
        ) : (
          <div className="lx-cal__undated-list">
            {undated.map((c) => (
              <Chip key={c.id} card={c} onOpen={onOpen} today={today} />
            ))}
          </div>
        )}
      </details>
    </section>
  );
}
