// The agenda's toolbar: season, type and search filters.

import { useId, useMemo } from 'react';
import type { EventAgenda } from '../data/eventsApi';
import { Icon } from './icons';
import type { Filters } from '../data/eventFilters';

export function Toolbar({
  agenda,
  filters,
  onChange,
}: {
  agenda: EventAgenda;
  filters: Filters;
  onChange: (next: Partial<Filters>) => void;
}) {
  const ids = { q: useId(), season: useId(), type: useId(), videos: useId() };
  const seasons = useMemo(() => [...new Set(agenda.past.map((e) => e.season))], [agenda]);
  const types = useMemo(() => [...new Set(agenda.past.map((e) => e.type))].sort((a, b) => a.localeCompare(b, 'pt')), [agenda]);

  return (
    <div className="events-tools" role="search">
      <label className="control" htmlFor={ids.q}>
        <span className="sr-only">Procurar no arquivo</span>
        <Icon name="search" />
        <input
          id={ids.q}
          type="search"
          placeholder="Procurar por nome ou local"
          value={filters.q}
          onChange={(e) => onChange({ q: e.target.value })}
        />
      </label>
      <label className="control control--select" htmlFor={ids.season}>
        <span className="sr-only">Época</span>
        <select id={ids.season} value={filters.season} onChange={(e) => onChange({ season: e.target.value })}>
          <option value="">Todas as épocas</option>
          {seasons.map((s) => (
            <option key={s} value={s}>
              {s.replace('-', '–')}
            </option>
          ))}
        </select>
      </label>
      <label className="control control--select" htmlFor={ids.type}>
        <span className="sr-only">Tipo</span>
        <select id={ids.type} value={filters.type} onChange={(e) => onChange({ type: e.target.value })}>
          <option value="">Todos os tipos</option>
          {types.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
      </label>
      <label className="check" htmlFor={ids.videos}>
        <input id={ids.videos} type="checkbox" checked={filters.videos} onChange={(e) => onChange({ videos: e.target.checked })} />
        Só com vídeos
      </label>
    </div>
  );
}
