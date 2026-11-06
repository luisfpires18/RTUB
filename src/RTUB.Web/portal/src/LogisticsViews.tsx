import { useState } from 'react';
import { Icon } from './icons';
import { deadline } from './LogisticsCard';
import { day, PRIORITY, STATUS, type Card, type List, type Priority, type Status } from './logisticsApi';

/**
 * Task 070 (issue #250): the board's list and calendar views and the summary they share with the Kanban. Every view
 * gets the same cards (the board's active cards the server sent - only what the caller may see - after the same
 * filters) and opens the same card workspace; nothing here writes, so every change goes through the card, under the
 * card's rules.
 */

export type Row = { card: Card; list: List };
export type Sort = 'due' | 'priority' | 'status' | 'title';

/** Today in Portugal, "yyyy-mm-dd" (deadlines are Portuguese calendar days). */
export const lisbonToday = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Lisbon' }).format(new Date());
const dueDay = (c: Card) => c.dueDate?.slice(0, 10) ?? null;
export const isLate = (c: Card, today = lisbonToday()) => c.status !== 'Done' && !!dueDay(c) && dueDay(c)! < today;

const PRIORITY_ORDER: Priority[] = ['Urgent', 'High', 'Normal', 'Low'];
const STATUS_ORDER: Status[] = ['Todo', 'InProgress', 'Blocked', 'Done'];
const pt = new Intl.Collator('pt-PT', { sensitivity: 'base' });
const HEX = /^#[0-9a-f]{6}$/i;

export function sortRows(rows: Row[], sort: Sort): Row[] {
  const byTitle = (a: Row, b: Row) => pt.compare(a.card.title, b.card.title);
  const by: Record<Sort, (a: Row, b: Row) => number> = {
    // Undated last; ties by title.
    due: (a, b) => (dueDay(a.card) ?? '9999').localeCompare(dueDay(b.card) ?? '9999') || byTitle(a, b),
    priority: (a, b) => PRIORITY_ORDER.indexOf(a.card.priority) - PRIORITY_ORDER.indexOf(b.card.priority) || byTitle(a, b),
    status: (a, b) => STATUS_ORDER.indexOf(a.card.status) - STATUS_ORDER.indexOf(b.card.status) || byTitle(a, b),
    title: byTitle,
  };
  return [...rows].sort(by[sort]);
}

/**
 * The board's numbers. Board-wide = every active card of the board the caller sees (archived cards never count);
 * "Atrasados" = a deadline before today in Portugal, not done; "Sem responsável" = not done, no responsible member.
 */
export function Summary({ all, shown, filtering }: { all: Card[]; shown: number; filtering: boolean }) {
  const today = lisbonToday();
  const done = all.filter((c) => c.status === 'Done').length;
  const metrics = [
    { label: 'Feitos', value: `${done}/${all.length}`, title: 'Cartões no estado Feito, do total de cartões ativos do quadro.' },
    { label: 'Atrasados', value: all.filter((c) => isLate(c, today)).length, title: 'Prazo antes de hoje (hora de Portugal) e ainda não feitos.', tone: 'late' },
    { label: 'Bloqueados', value: all.filter((c) => c.status === 'Blocked').length, title: 'Cartões no estado Bloqueado.', tone: 'blocked' },
    { label: 'Sem responsável', value: all.filter((c) => !c.assignedTo && c.status !== 'Done').length, title: 'Por fazer e sem responsável.', tone: 'warn' },
  ];
  const progress = all.length ? Math.round((done / all.length) * 100) : 0;
  return (
    <section className="lx-summary" aria-label="Resumo do quadro">
      <dl className="lx-summary__metrics">
        {metrics.map((m) => (
          <div key={m.label} className={`lx-summary__metric${m.tone && m.value ? ` lx-summary__metric--${m.tone}` : ''}`} title={m.title}>
            <dt>{m.label}</dt>
            <dd>{m.value}</dd>
          </div>
        ))}
      </dl>
      <span className="lx-summary__bar" aria-hidden="true">
        <span style={{ width: `${progress}%` }} />
      </span>
      <p className="lx-summary__note">
        {filtering ? (
          <>
            <strong>
              A mostrar {shown} de {all.length}
            </strong>{' '}
            (filtros ativos). Os números acima são do quadro todo.
          </>
        ) : (
          'Números do quadro todo; os cartões arquivados não contam.'
        )}
      </p>
    </section>
  );
}

function Due({ card }: { card: Card }) {
  const d = deadline(card.dueDate, card.status);
  if (!card.dueDate) return <span className="lx-muted">Sem prazo</span>;
  return (
    <span className={`lx-due${d ? ` lx-due--${d.tone}` : ''}`}>
      {day(card.dueDate)}
      {d && <small className="lx-due__note">{d.text}</small>}
    </span>
  );
}

/** Lista: one row per card, sortable; on phones each row becomes a stacked card. */
export function ListView({ rows, sort, onSort, onOpen }: { rows: Row[]; sort: Sort; onSort: (s: Sort) => void; onOpen: (id: number) => void }) {
  const head = (key: Sort, label: string) => (
    <th scope="col" aria-sort={sort === key ? 'ascending' : 'none'}>
      <button type="button" className="lx-table__sort" aria-pressed={sort === key} onClick={() => onSort(key)}>
        {label}
        {sort === key && <Icon name="down" />}
      </button>
    </th>
  );
  return (
    <div className="lx-table-wrap">
      <table className="lx-table">
        <caption className="sr-only">Cartões do quadro, ordenados por {{ due: 'prazo', priority: 'prioridade', status: 'estado', title: 'título' }[sort]}</caption>
        <thead>
          <tr>
            {head('title', 'Cartão')}
            {head('status', 'Estado')}
            <th scope="col">Coluna</th>
            <th scope="col">Responsável</th>
            {head('priority', 'Prioridade')}
            {head('due', 'Prazo')}
            <th scope="col">Checklist</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(({ card: c, list }) => (
            <tr key={c.id} className={`lx-row lx-row--${c.status.toLowerCase()}`}>
              <th scope="row" className="lx-row__title">
                <button type="button" className="lx-row__open" onClick={() => onOpen(c.id)}>
                  {c.title}
                </button>
                {c.labels.length > 0 && (
                  <span className="lx-row__labels">
                    {c.labels.map((l) => (
                      <span key={l.text} className="lx-label">
                        <span className="lx-label__dot" style={{ background: HEX.test(l.color) ? l.color : '#6f42c1' }} aria-hidden="true" />
                        {l.text}
                      </span>
                    ))}
                  </span>
                )}
              </th>
              <td data-label="Estado">
                <span className={`lx-status lx-view__status--${c.status.toLowerCase()}`}>{STATUS[c.status].label}</span>
              </td>
              <td data-label="Coluna">{list.name}</td>
              <td data-label="Responsável">
                {c.assignedTo ? c.assignedTo.displayName : <span className="lx-warn-text">Sem responsável</span>}
                {c.mine && <span className="lx-chip lx-chip--mine">Meu</span>}
              </td>
              <td data-label="Prioridade">
                {c.priority === 'Normal' ? <span className="lx-muted">Normal</span> : <span className={`lx-prio lx-prio--${c.priority.toLowerCase()}`}>{PRIORITY[c.priority]}</span>}
              </td>
              <td data-label="Prazo">
                <Due card={c} />
              </td>
              <td data-label="Checklist">{c.checklistTotal > 0 ? `${c.checklistDone}/${c.checklistTotal}` : <span className="lx-muted">—</span>}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

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
        <button type="button" className="icon-btn icon-btn--sm" onClick={() => onMonth(shiftMonth(month, -1))}>
          <Icon name="chevronLeft" />
          <span className="sr-only">Mês anterior</span>
        </button>
        <h2 className="lx-cal__title" aria-live="polite">
          {label}
        </h2>
        <button type="button" className="icon-btn icon-btn--sm" onClick={() => onMonth(shiftMonth(month, 1))}>
          <Icon name="chevronRight" />
          <span className="sr-only">Mês seguinte</span>
        </button>
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
