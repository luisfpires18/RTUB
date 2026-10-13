import { useCallback, useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { getGallery, getGalleryItem, type GalleryFilters, type GalleryItem, type GalleryTimeline } from './api';
import { Loading } from './App';
import { EditDialog, UploadDialog } from './GalleryManage';
import { Icon } from './icons';

const monthName = (month: number) =>
  new Intl.DateTimeFormat('pt-PT', { month: 'long' }).format(new Date(2000, month - 1, 1));

/** "12 de março de 2025", "março de 2025" or "2025": only the precision the item has. */
export function dateLabel(item: Pick<GalleryItem, 'year' | 'month' | 'day'>) {
  if (!item.month) return String(item.year);
  const month = `${monthName(item.month)} de ${item.year}`;
  return item.day ? `${item.day} de ${month}` : month;
}

type MonthGroup = { key: string; label: string; items: GalleryItem[] };
type YearGroup = { year: number; months: MonthGroup[] };

/** The server already sends newest first; this only cuts the list into years and months. */
export function groupTimeline(items: GalleryItem[]): YearGroup[] {
  const years: YearGroup[] = [];
  for (const item of items) {
    let year = years[years.length - 1];
    if (year?.year !== item.year) years.push((year = { year: item.year, months: [] }));
    const key = String(item.month ?? 0);
    let month = year.months[year.months.length - 1];
    if (month?.key !== key) {
      year.months.push(
        (month = { key, label: item.month ? monthName(item.month) : 'Sem mês indicado', items: [] }),
      );
    }
    month.items.push(item);
  }
  return years;
}

const itemFromUrl = () => Number(new URLSearchParams(location.search).get('item')) || undefined;

function setItemInUrl(id?: number) {
  const url = new URL(location.href);
  if (id) url.searchParams.set('item', String(id));
  else url.searchParams.delete('item');
  history.replaceState(null, '', url.pathname + url.search);
}

/**
 * /gallery - the RTUB through the years, newest first. Visitors see the public photos; signed-in
 * members also see members-only ones (badged) and who is in each photo. Everything is decided by
 * GET /api/gallery from the session. Members upload here, and the uploader, Admin or Owner edit (015).
 */
export default function Gallery() {
  const [data, setData] = useState<GalleryTimeline>();
  const [items, setItems] = useState<GalleryItem[]>([]);
  const [failed, setFailed] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [filters, setFilters] = useState<GalleryFilters>({});
  const [search, setSearch] = useState('');
  const [open, setOpen] = useState<GalleryItem>();
  const [managing, setManaging] = useState<'upload' | number>();

  useEffect(() => {
    document.title = 'Galeria · RTUB';
  }, []);

  const load = useCallback((page: number, current: GalleryFilters) => {
    setFailed(false);
    if (page > 1) setLoadingMore(true);
    getGallery(page, current).then(
      (timeline) => {
        setData(timeline);
        setItems((previous) => (page === 1 ? timeline.items : [...previous, ...timeline.items]));
        setLoadingMore(false);
      },
      () => {
        setFailed(true);
        setLoadingMore(false);
      },
    );
  }, []);

  useEffect(() => load(1, filters), [load, filters]);

  // Typing searches after a short pause; the other filters apply at once.
  useEffect(() => {
    const q = search.trim() || undefined;
    if (q === filters.q) return;
    const timer = setTimeout(() => setFilters((f) => ({ ...f, q })), 350);
    return () => clearTimeout(timer);
  }, [search, filters.q]);

  // A shared ?item= link opens that photo, even when it is not on the first page.
  useEffect(() => {
    const id = itemFromUrl();
    if (id) getGalleryItem(id).then(setOpen, () => setItemInUrl());
  }, []);

  const show = (item?: GalleryItem) => {
    setOpen(item);
    setItemInUrl(item?.id);
  };

  const groups = useMemo(() => groupTimeline(items), [items]);
  const filtered = Boolean(filters.year || filters.q || filters.person);

  return (
    <section className="page wrap gallery-page" aria-labelledby="gallery-title">
      <header className="page__head gallery-page__head">
        <div>
          <p className="eyebrow">Memórias</p>
          <h1 id="gallery-title" className="page__title">
            Galeria
          </h1>
          <p className="page__lead">Atuações, viagens e noites de estrada: a RTUB ao longo dos anos, do mais recente para trás.</p>
        </div>
        {data?.isMember && (
          <button type="button" className="btn btn--primary btn--sm" onClick={() => setManaging('upload')}>
            <Icon name="upload" />
            Carregar foto ou vídeo
          </button>
        )}
      </header>

      {data && (
        <Toolbar
          timeline={data}
          filters={filters}
          search={search}
          onSearch={setSearch}
          onFilters={(next) => setFilters((f) => ({ ...f, ...next }))}
        />
      )}

      {failed && !loadingMore ? (
        <div className="notice" role="status">
          <p>Não foi possível carregar a galeria agora.</p>
          <button type="button" className="btn btn--ghost btn--sm" onClick={() => load(1, filters)}>
            Tentar novamente
          </button>
        </div>
      ) : !data ? (
        <Loading label="A carregar a galeria…" />
      ) : items.length === 0 ? (
        <div className="notice" role="status">
          <p>{filtered ? 'Nada encontrado com estes filtros.' : 'Ainda não há fotografias na galeria.'}</p>
          {filtered && (
            <button
              type="button"
              className="btn btn--ghost btn--sm"
              onClick={() => {
                setSearch('');
                setFilters({});
              }}
            >
              Limpar filtros
            </button>
          )}
        </div>
      ) : (
        <>
          <ol className="timeline" aria-label="Linha do tempo">
            {groups.map((group) => (
              <li key={group.year} className="timeline__year">
                <h2 className="timeline__year-label">{group.year}</h2>
                {group.months.map((month) => (
                  <section key={month.key} className="timeline__month" aria-label={`${month.label} de ${group.year}`}>
                    <h3 className="timeline__month-label">{month.label}</h3>
                    <ul className="mosaic">
                      {month.items.map((item) => (
                        <li key={item.id}>
                          <Tile item={item} onOpen={() => show(item)} />
                        </li>
                      ))}
                    </ul>
                  </section>
                ))}
              </li>
            ))}
          </ol>

          {items.length < data.total && (
            <div className="gallery-page__more">
              <button
                type="button"
                className="btn btn--ghost"
                disabled={loadingMore}
                aria-busy={loadingMore}
                onClick={() => load(data.page + 1, filters)}
              >
                {loadingMore ? 'A carregar…' : 'Mostrar mais'}
              </button>
              <p className="note">
                {items.length} de {data.total}
              </p>
            </div>
          )}
        </>
      )}

      {open && (
        <Lightbox
          item={open}
          siblings={items}
          onMove={show}
          onClose={() => show(undefined)}
          onEdit={
            open.canEdit
              ? () => {
                  show(undefined);
                  setManaging(open.id);
                }
              : undefined
          }
        />
      )}
      {managing === 'upload' && <UploadDialog onClose={() => setManaging(undefined)} onDone={() => load(1, filters)} />}
      {typeof managing === 'number' && <EditDialog itemId={managing} onClose={() => setManaging(undefined)} onDone={() => load(1, filters)} />}
    </section>
  );
}

function Toolbar({
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

/** Natural aspect ratio, never cropped; a broken or missing file becomes a quiet placeholder. */
function Tile({ item, onOpen }: { item: GalleryItem; onOpen: () => void }) {
  const [broken, setBroken] = useState(!item.url);

  return (
    <button type="button" className="tile" onClick={onOpen} aria-label={`${item.title}, ${dateLabel(item)}`}>
      <span className="tile__frame">
        {broken ? (
          <span className="tile__missing">
            <Icon name="images" />
            Imagem indisponível
          </span>
        ) : item.type === 'video' ? (
          <>
            {/* #t=0.1 asks for the first frame: there are no stored thumbnails. */}
            <video
              className="tile__media"
              src={`${item.url}#t=0.1`}
              preload="metadata"
              muted
              playsInline
              onError={() => setBroken(true)}
            />
            <span className="tile__play" aria-hidden="true">
              <Icon name="play" />
            </span>
          </>
        ) : (
          <img className="tile__media" src={item.url!} alt="" loading="lazy" decoding="async" onError={() => setBroken(true)} />
        )}
        {item.membersOnly && (
          <span className="tile__badge">
            <Icon name="lock" />
            Membros
          </span>
        )}
      </span>
      <span className="tile__caption">
        <span className="tile__title">{item.title}</span>
        <span className="tile__date">{dateLabel(item)}</span>
      </span>
    </button>
  );
}

/** A native modal <dialog>: focus containment, Esc and an inert page come from the platform. */
function Lightbox({
  item,
  siblings,
  onMove,
  onClose,
  onEdit,
}: {
  item: GalleryItem;
  siblings: GalleryItem[];
  onMove: (item: GalleryItem) => void;
  onClose: () => void;
  /** The uploader, Admin or Owner (015). */
  onEdit?: () => void;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [broken, setBroken] = useState(false);
  const index = siblings.findIndex((s) => s.id === item.id);
  const previous = index > 0 ? siblings[index - 1] : undefined;
  const next = index >= 0 && index < siblings.length - 1 ? siblings[index + 1] : undefined;

  useEffect(() => {
    const d = dialog.current;
    if (d && !d.open) d.showModal();
  }, []);

  useEffect(() => setBroken(false), [item.id]);

  const onKey = (e: KeyboardEvent) => {
    if (e.key === 'ArrowLeft' && previous) onMove(previous);
    if (e.key === 'ArrowRight' && next) onMove(next);
  };

  return (
    <dialog
      ref={dialog}
      className="lightbox"
      aria-labelledby="lightbox-title"
      onClose={onClose}
      onCancel={(e) => {
        // Esc: close through React rather than waiting for the native close event.
        e.preventDefault();
        onClose();
      }}
      onKeyDown={onKey}
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <div className="lightbox__stage">
        {!item.url || broken ? (
          <p className="tile__missing">
            <Icon name="images" />
            Imagem indisponível
          </p>
        ) : item.type === 'video' ? (
          <video key={item.id} className="lightbox__media" src={item.url} controls playsInline preload="metadata" onError={() => setBroken(true)} />
        ) : (
          <img key={item.id} className="lightbox__media" src={item.url} alt={item.title} onError={() => setBroken(true)} />
        )}
      </div>

      <div className="lightbox__info">
        <div className="lightbox__text">
          <h2 id="lightbox-title" className="lightbox__title">
            {item.title}
          </h2>
          <p className="lightbox__date">
            {dateLabel(item)}
            {item.membersOnly && (
              <span className="tile__badge tile__badge--inline">
                <Icon name="lock" />
                Membros
              </span>
            )}
          </p>
          {item.people.length > 0 && (
            <ul className="chips" aria-label="Quem aparece">
              {item.people.map((p) => (
                <li key={p.id} className="chip">
                  {p.name}
                </li>
              ))}
            </ul>
          )}
        </div>
        <div className="lightbox__actions">
          {onEdit && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={onEdit}>
              <Icon name="pencil" />
              Editar
            </button>
          )}
          {item.url && (
            <a className="btn btn--ghost btn--sm" href={item.url} target="_blank" rel="noopener noreferrer">
              <Icon name="external" />
              Abrir original
            </a>
          )}
          {index >= 0 && siblings.length > 1 && (
            <span className="lightbox__count">
              {index + 1} / {siblings.length}
            </span>
          )}
        </div>
      </div>

      <button type="button" className="icon-btn lightbox__close" onClick={onClose}>
        <Icon name="close" />
        <span className="sr-only">Fechar</span>
      </button>
      {previous && (
        <button type="button" className="icon-btn lightbox__nav lightbox__nav--prev" onClick={() => onMove(previous)}>
          <Icon name="prev" />
          <span className="sr-only">Anterior</span>
        </button>
      )}
      {next && (
        <button type="button" className="icon-btn lightbox__nav lightbox__nav--next" onClick={() => onMove(next)}>
          <Icon name="next" />
          <span className="sr-only">Seguinte</span>
        </button>
      )}
    </dialog>
  );
}
