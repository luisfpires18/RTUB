// Gallery filter bar: year, month, event, search and the upload button.
import { useId } from 'react';
import { type GalleryFilters, type GalleryTimeline } from '../data/api';
import { Icon } from './icons';

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
      <label className="control" htmlFor={ids.search}>
        <span className="sr-only">Procurar na galeria</span>
        <Icon name="search" />
        <input id={ids.search} type="search" placeholder="Procurar por título" value={search} onChange={(e) => onSearch(e.target.value)} />
      </label>
      <label className="control control--select" htmlFor={ids.year}>
        <span className="sr-only">Ano</span>
        <select
          id={ids.year}
          value={filters.year ?? ''}
          onChange={(e) => onFilters({ year: Number(e.target.value) || undefined })}
        >
          <option value="">Todos os anos</option>
          {timeline.years.map((y) => (
            <option key={y} value={y}>
              {y}
            </option>
          ))}
        </select>
      </label>
      {timeline.isMember && timeline.people.length > 0 && (
        <label className="control control--select" htmlFor={ids.person}>
          <span className="sr-only">Quem aparece</span>
          <select id={ids.person} value={filters.person ?? ''} onChange={(e) => onFilters({ person: e.target.value || undefined })}>
            <option value="">Todas as pessoas</option>
            {timeline.people.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </label>
      )}
    </div>
  );
}
