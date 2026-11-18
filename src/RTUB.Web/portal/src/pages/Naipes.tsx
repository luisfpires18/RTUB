import { useEffect, useRef, useState } from 'react';
import { portal } from '../data/content';
import { Icon } from '../components/icons';
import { naipesApi, type NaipeBoard, type NaipeItem } from '../data/naipesApi';
import { Notice, SignInNotice } from '../components/Notice';
import { PAGE_SIZES, type Editing } from '../data/naipesShared';
import { NaipeCard } from '../components/NaipeCard';
import { ViewDialog } from '../components/NaipeViewDialog';
import { EditDialog, DeleteDialog } from '../components/NaipeEditDialogs';
import { Loading } from '../components/ui/Loading';
import { Pager } from '../components/ui/Pager';

/**
 * /naipes. Each instrument's teaching videos and images: pick an instrument, search,
 * open one to watch it and comment. Any member adds content and edits or deletes what they added; Admin and Owner
 * manage everything and the instrument settings (/naipes/config). The server decides every rule (NaipeBoardService).
 */
export default function Naipes() {
  const initial = new URLSearchParams(location.search);
  const [instrument, setInstrument] = useState(initial.get('instrument') ?? '');
  const [text, setText] = useState(initial.get('q') ?? '');
  const [q, setQ] = useState(text);
  const [board, setBoard] = useState<NaipeBoard | 'signin' | null>();
  const [version, setVersion] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [open, setOpen] = useState<NaipeItem>();
  const [editing, setEditing] = useState<Editing>();
  const [deleting, setDeleting] = useState<NaipeItem>();
  const reveal = useRef<number>(undefined); // after a save: show the page that holds that item (a new one goes last)

  useEffect(() => {
    document.title = 'Naipes · RTUB';
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => setQ(text.trim()), 250);
    return () => clearTimeout(timer);
  }, [text]);

  useEffect(() => {
    const url = new URL(location.href);
    instrument ? url.searchParams.set('instrument', instrument) : url.searchParams.delete('instrument');
    q ? url.searchParams.set('q', q) : url.searchParams.delete('q');
    history.replaceState(null, '', url.pathname + url.search);
    let live = true; // a slower answer for an older query never replaces the newer one
    naipesApi.board(instrument, q).then((o) => {
      if (!live) return;
      if (o.kind === 'ok' && reveal.current !== undefined) {
        const index = o.data.items.findIndex((i) => i.id === reveal.current);
        if (index >= 0) setPage(Math.floor(index / pageSize) + 1);
        reveal.current = undefined;
      }
      setBoard(o.kind === 'ok' ? o.data : o.kind === 'signin' ? 'signin' : null);
    });
    return () => {
      live = false;
    };
  }, [instrument, q, version]);

  const data = board && board !== 'signin' ? board : null;
  const reload = () => setVersion((v) => v + 1);
  const pick = (value: string) => {
    setInstrument(value);
    setPage(1);
  };
  const pages = data ? Math.max(1, Math.ceil(data.items.length / pageSize)) : 1;
  const shown = data ? data.items.slice((Math.min(page, pages) - 1) * pageSize, Math.min(page, pages) * pageSize) : [];
  const chosen = data?.instruments.find((i) => i.value === data.instrument);

  return (
    <section className="page wrap events-page naipes-page" aria-labelledby="naipes-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Área de membros</p>
          <h1 id="naipes-title" className="page__title">
            Naipes
          </h1>
          <p className="page__lead">Vídeos e imagens para estudar cada instrumento, partilhados pelos membros.</p>
        </div>
        {data && (
          <div className="events-page__actions">
            {data.canConfigure && (
              <a className="btn btn--ghost btn--sm" href={portal.naipesConfig}>
                <Icon name="pencil" />
                Configurar
              </a>
            )}
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setEditing({ kind: 'new', isVideo: true })}>
              <Icon name="video" />
              Adicionar vídeo
            </button>
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setEditing({ kind: 'new', isVideo: false })}>
              <Icon name="images" />
              Adicionar imagem
            </button>
          </div>
        )}
      </header>

      {board === undefined ? (
        <Loading label="A carregar os naipes…" />
      ) : board === 'signin' ? (
        <SignInNotice path={portal.naipes}>Os naipes são da área de membros.</SignInNotice>
      ) : board === null ? (
        <Notice>Não foi possível carregar os naipes. Tenta outra vez daqui a pouco.</Notice>
      ) : (
        <>
          {board.instruments.length === 0 ? (
            <p className="notice">Ainda não há instrumentos visíveis.</p>
          ) : (
            <ul className="naipe-picker" aria-label="Instrumentos">
              {board.instruments.map((i) => (
                <li key={i.value}>
                  <button
                    type="button"
                    className={i.value === board.instrument ? 'naipe-pick is-active' : 'naipe-pick'}
                    aria-pressed={i.value === board.instrument}
                    onClick={() => pick(i.value)}
                  >
                    {i.pictureUrl ? <img src={i.pictureUrl} alt="" width="56" height="56" loading="lazy" /> : <Icon name="music" />}
                    <span>{i.label}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}

          {!board.instrument ? (
            <p className="notice naipe-empty">Escolhe um instrumento para veres os vídeos e as imagens dele.</p>
          ) : board.totalForInstrument === 0 ? (
            <p className="notice naipe-empty">Ainda não há vídeos nem imagens de {chosen?.label ?? 'este instrumento'}. Podes ser o primeiro a juntar.</p>
          ) : (
            <>
              <label className="control naipe-search">
                <Icon name="search" />
                <span className="sr-only">Procurar</span>
                <input type="search" placeholder="Procurar no título ou na descrição" value={text} onChange={(e) => (setText(e.target.value), setPage(1))} />
              </label>
              {board.items.length === 0 ? (
                <p className="notice naipe-empty">Nada encontrado para “{q}”.</p>
              ) : (
                <>
                  <ul className="naipe-grid">
                    {shown.map((item) => (
                      <li key={item.id}>
                        <NaipeCard item={item} onOpen={() => setOpen(item)} onEdit={() => setEditing({ kind: 'edit', item })} onDelete={() => setDeleting(item)} />
                      </li>
                    ))}
                  </ul>
                  <Pager
                    block="naipe-pager"
                    ariaLabel="Páginas"
                    total={board.items.length}
                    itemLabel={board.items.length === 1 ? 'conteúdo' : 'conteúdos'}
                    page={Math.min(page, pages)}
                    pages={pages}
                    pageSize={pageSize}
                    sizes={PAGE_SIZES}
                    onPage={setPage}
                    onPageSize={(size) => (setPageSize(size), setPage(1))}
                  />
                </>
              )}
            </>
          )}
        </>
      )}

      {open && <ViewDialog item={open} onClose={() => setOpen(undefined)} onChanged={reload} />}
      {editing && data && (
        <EditDialog board={data} editing={editing} onClose={() => setEditing(undefined)} onSaved={(item) => ((reveal.current = item.id), pick(item.instrument), reload())} />
      )}
      {deleting && <DeleteDialog item={deleting} onClose={() => setDeleting(undefined)} onDone={reload} />}
    </section>
  );
}
