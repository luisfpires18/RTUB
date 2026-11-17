// The board's list view and the summary strip.
import { Icon } from './icons';
import { PRIORITY, STATUS, type Card } from '../data/logisticsApi';
import { day, lisbonToday } from '../data/format';
import { deadline } from '../data/logisticsDates';
import { isLate, type Row, type Sort } from '../data/logisticsRows';

/**
 * Task 070 (issue #250): the board's list and calendar views and the summary they share with the Kanban. Every view
 * gets the same cards (the board's active cards the server sent - only what the caller may see - after the same
 * filters) and opens the same card workspace; nothing here writes, so every change goes through the card, under the
 * card's rules.
 */

const HEX = /^#[0-9a-f]{6}$/i;

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
