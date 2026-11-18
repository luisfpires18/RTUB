import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { getGallery, getGalleryItem, type GalleryFilters, type GalleryItem, type GalleryTimeline } from '../data/api';
import { Loading, useLatest } from '../components/AppShared';
import { EditDialog, UploadDialog } from '../components/GalleryManage';
import type { UploadEvent } from '../data/galleryUploadApi';
import { Icon } from '../components/icons';
import { RetryNotice } from '../components/Notice';
import { Toolbar } from '../components/GalleryToolbar';
import { Tile } from '../components/GalleryTile';
import { Lightbox } from '../components/GalleryLightbox';
import { groupTimeline } from '../data/galleryTimeline';


/** ?upload=1&event=&date=&title= (from /pending-actions → Galeria → Adicionar) opens the upload for that event. */
function uploadEventFromUrl(): UploadEvent | undefined {
  const q = new URLSearchParams(location.search);
  const id = Number(q.get('event'));
  if (q.get('upload') !== '1' || !Number.isInteger(id) || id <= 0) return undefined;
  const date = q.get('date') ?? '';
  return { id, title: q.get('title') ?? '', date: /^\d{4}-\d{2}-\d{2}$/.test(date) ? date : '' };
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
 * GET /api/gallery from the session. Members upload here, and the uploader, Admin or Owner edit.
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
  const [uploadEvent, setUploadEvent] = useState<UploadEvent>();
  const askedUpload = useRef(false);

  useEffect(() => {
    document.title = 'Galeria · RTUB';
  }, []);

  const latest = useLatest();
  const load = useCallback((page: number, current: GalleryFilters) => {
    setFailed(false);
    if (page > 1) setLoadingMore(true);
    latest(
      getGallery(page, current).then(
        (timeline) => ({ timeline }),
        () => ({ timeline: null }),
      ),
      ({ timeline }) => {
        setLoadingMore(false);
        if (!timeline) return setFailed(true);
        // The facets (years, people) come with the first page only; later pages keep the ones in hand.
        setData((previous) =>
          page === 1 || !previous ? timeline : { ...timeline, years: previous.years, people: previous.people },
        );
        setItems((previous) => (page === 1 ? timeline.items : [...previous, ...timeline.items]));
      },
    );
  }, [latest]);

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

  // The pending-actions link: open the upload once the page knows the member may upload, then drop the query.
  useEffect(() => {
    if (!data || askedUpload.current) return;
    askedUpload.current = true;
    const event = uploadEventFromUrl();
    if (!event) return;
    history.replaceState(null, '', location.pathname);
    if (data.isMember) {
      setUploadEvent(event);
      setManaging('upload');
    }
  }, [data]);

  const show = (item?: GalleryItem) => {
    setOpen(item);
    setItemInUrl(item?.id);
  };

  const groups = useMemo(() => groupTimeline(items), [items]);
  const filtered = Boolean(filters.year || filters.q || filters.person);

  return (
    <section className="page wrap gallery-page" aria-labelledby="gallery-title">
      <header className="page__head events-page__head">
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
        <RetryNotice onRetry={() => load(1, filters)}>Não foi possível carregar a galeria agora.</RetryNotice>
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
      {managing === 'upload' && (
        <UploadDialog
          event={uploadEvent}
          onClose={() => {
            setManaging(undefined);
            setUploadEvent(undefined);
          }}
          onDone={() => load(1, filters)}
        />
      )}
      {typeof managing === 'number' && <EditDialog itemId={managing} onClose={() => setManaging(undefined)} onDone={() => load(1, filters)} />}
    </section>
  );
}
