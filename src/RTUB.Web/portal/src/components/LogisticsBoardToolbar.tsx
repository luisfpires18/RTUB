// The board page toolbar: search, status buttons, filters, order and jump bar.
import { Icon } from './icons';
import { PRIORITIES_URGENT_FIRST, PRIORITY, STATUS, STATUSES, type Board, type Priority, type Status } from '../data/logisticsApi';
import type { Due, View } from '../data/logisticsBoardTypes';
import type { Sort } from '../data/logisticsRows';

/** The board page search, status buttons, filters, order and list jump bar. */
export function LogisticsBoardToolbar({
  ids, data, view, text, setText, status, setStatus, label, setLabel, who, setWho, prio, setPrio, due, setDue, sort, setSort,
  moreFilters, setMoreFilters, people, filtering, clear, columns,
}: {
  ids: Record<string, string>; data: Board; view: View; text: string; setText: (v: string) => void; status: Status | ''; setStatus: (v: Status | '') => void;
  label: string; setLabel: (v: string) => void; who: string; setWho: (v: string) => void; prio: Priority | ''; setPrio: (v: Priority | '') => void;
  due: Due; setDue: (v: Due) => void; sort: Sort; setSort: (v: Sort) => void; moreFilters: boolean; setMoreFilters: (v: boolean) => void;
  people: string[]; filtering: boolean; clear: () => void; columns: { current: Record<number, HTMLElement | null> };
}) {
  return (
    <div className="lx-toolbar" role="search">
      <label className="control control--sm" htmlFor={ids.q}>
        <span className="sr-only">Procurar cartão</span>
        <Icon name="search" />
        <input id={ids.q} type="search" placeholder="Procurar cartão" value={text} onChange={(e) => setText(e.target.value)} />
      </label>
      <div className="lx-status-filter" role="group" aria-label="Estado">
        <button type="button" aria-pressed={status === ''} onClick={() => setStatus('')}>
          Todos
        </button>
        {STATUSES.map((s) => (
          <button key={s} type="button" className={`lx-status-filter--${s.toLowerCase()}`} aria-pressed={status === s} onClick={() => setStatus(status === s ? '' : s)}>
            {STATUS[s].label}
          </button>
        ))}
      </div>
      <button
        type="button"
        className="lx-more-toggle"
        aria-expanded={moreFilters}
        aria-controls={`${ids.who}-more`}
        onClick={() => setMoreFilters(!moreFilters)}
      >
        <Icon name="chevronDown" />
        Filtros{(who || prio || due || label) && ` (${[who, prio, due, label].filter(Boolean).length})`}
      </button>
      <div id={`${ids.who}-more`} className={moreFilters ? 'lx-toolbar__more lx-toolbar__more--open' : 'lx-toolbar__more'}>
        <label className="control control--select control--sm" htmlFor={ids.who}>
          <span className="sr-only">Responsável</span>
          <select id={ids.who} value={who} onChange={(e) => setWho(e.target.value)}>
            <option value="">Qualquer responsável</option>
            <option value="mine">Os meus cartões</option>
            <option value="none">Sem responsável</option>
            {people.map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
        </label>
        <label className="control control--select control--sm" htmlFor={ids.prio}>
          <span className="sr-only">Prioridade</span>
          <select id={ids.prio} value={prio} onChange={(e) => setPrio(e.target.value as Priority | '')}>
            <option value="">Qualquer prioridade</option>
            {PRIORITIES_URGENT_FIRST.map((p) => (
              <option key={p} value={p}>
                {PRIORITY[p]}
              </option>
            ))}
          </select>
        </label>
        <label className="control control--select control--sm" htmlFor={ids.due}>
          <span className="sr-only">Prazo</span>
          <select id={ids.due} value={due} onChange={(e) => setDue(e.target.value as Due)}>
            <option value="">Qualquer prazo</option>
            <option value="late">Atrasados</option>
            <option value="soon">Próximos 7 dias</option>
            <option value="none">Sem prazo</option>
          </select>
        </label>
        {data.labels.length > 0 && (
          <label className="control control--select control--sm" htmlFor={ids.label}>
            <span className="sr-only">Etiqueta</span>
            <select id={ids.label} value={label} onChange={(e) => setLabel(e.target.value)}>
              <option value="">Todas as etiquetas</option>
              {data.labels.map((l) => (
                <option key={l} value={l}>
                  {l}
                </option>
              ))}
            </select>
          </label>
        )}
        {view === 'list' && (
          <label className="control control--select control--sm" htmlFor={ids.sort}>
            <span className="sr-only">Ordenar</span>
            <select id={ids.sort} value={sort} onChange={(e) => setSort(e.target.value as Sort)}>
              <option value="due">Ordenar por prazo</option>
              <option value="priority">Ordenar por prioridade</option>
              <option value="status">Ordenar por estado</option>
              <option value="title">Ordenar por título</option>
            </select>
          </label>
        )}
      </div>
      {filtering && (
        <button type="button" className="btn btn--ghost btn--sm" onClick={clear}>
          <Icon name="close" />
          Limpar filtros
        </button>
      )}
      {view === 'board' && data.lists.length > 1 && (
        <nav className="lx-jump" aria-label="Ir para a lista">
          {data.lists.map((l) => (
            <button key={l.id} type="button" onClick={() => columns.current[l.id]?.scrollIntoView({ behavior: 'smooth', inline: 'start', block: 'nearest' })}>
              {l.name}
            </button>
          ))}
        </nav>
      )}
    </div>
  );
}
