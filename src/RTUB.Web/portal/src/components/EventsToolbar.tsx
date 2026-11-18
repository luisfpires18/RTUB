// The agenda's toolbar: season, type and search filters.

import { useId, useMemo } from 'react';
import type { EventAgenda } from '../data/eventsApi';
import { SearchField, SelectField, ToolsBar } from './ui/SearchTools';
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
    <ToolsBar>
      <SearchField id={ids.q} label="Procurar no arquivo" placeholder="Procurar por nome ou local" value={filters.q} onChange={(q) => onChange({ q })} />
      <SelectField id={ids.season} label="Época" value={filters.season} onChange={(season) => onChange({ season })}>
        <option value="">Todas as épocas</option>
        {seasons.map((s) => (
          <option key={s} value={s}>
            {s.replace('-', '–')}
          </option>
        ))}
      </SelectField>
      <SelectField id={ids.type} label="Tipo" value={filters.type} onChange={(type) => onChange({ type })}>
        <option value="">Todos os tipos</option>
        {types.map((t) => (
          <option key={t} value={t}>
            {t}
          </option>
        ))}
      </SelectField>
      <label className="check" htmlFor={ids.videos}>
        <input id={ids.videos} type="checkbox" checked={filters.videos} onChange={(e) => onChange({ videos: e.target.checked })} />
        Só com vídeos
      </label>
    </ToolsBar>
  );
}
