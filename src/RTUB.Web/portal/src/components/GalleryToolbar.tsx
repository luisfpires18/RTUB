// Gallery filter bar: year, month, event, search and the upload button.
import { useId } from 'react';
import { SearchField, SelectField } from './ui/SearchTools';
import { type GalleryFilters, type GalleryTimeline } from '../data/api';

export function Toolbar({
  timeline,
  filters,
  search,
  onSearch,
  onFilters,
}: {
  timeline: GalleryTimeline;
  filters: GalleryFilters;
  search: string;
  onSearch: (value: string) => void;
  onFilters: (next: GalleryFilters) => void;
}) {
  const ids = { search: useId(), year: useId(), person: useId() };
  return (
    <div className="gallery-tools" role="search">
      <SearchField id={ids.search} label="Procurar na galeria" placeholder="Procurar por título" value={search} onChange={onSearch} />
      <SelectField id={ids.year} label="Ano" value={String(filters.year ?? '')} onChange={(v) => onFilters({ year: Number(v) || undefined })}>
        <option value="">Todos os anos</option>
        {timeline.years.map((y) => (
          <option key={y} value={y}>
            {y}
          </option>
        ))}
      </SelectField>
      {timeline.isMember && timeline.people.length > 0 && (
        <SelectField id={ids.person} label="Quem aparece" value={filters.person ?? ''} onChange={(v) => onFilters({ person: v || undefined })}>
          <option value="">Todas as pessoas</option>
          {timeline.people.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </SelectField>
      )}
    </div>
  );
}
