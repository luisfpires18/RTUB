import { useCallback, useEffect, useState } from 'react';
import { Loading } from '../components/AppShared';
import { Icon } from '../components/icons';
import { AlbumForm } from '../components/MusicAlbumForm';
import { Badge, ConfirmDialog, IconButton } from '../components/MusicUi';
import { musicApi, type AlbumSummary, type MusicPermissions } from '../data/musicApi';
import { StatisticsDialog } from '../components/MusicStatistics';

/**
 * /music - the discography (React track 006). One grid for every album the caller may see:
 * visitors get the public ones, members also the private ones and the exclusive albums they are
 * listed on. Managers get the album form and delete; any member gets the statistics.
 */
export default function Music() {
  const [state, setState] = useState<{ albums?: AlbumSummary[]; permissions?: MusicPermissions; failed?: boolean }>({});
  const [editing, setEditing] = useState<number | null | undefined>(undefined);
  const [deleting, setDeleting] = useState<AlbumSummary | null>(null);
  const [deleteState, setDeleteState] = useState<{ busy: boolean; error?: string }>({ busy: false });
  const [showStats, setShowStats] = useState(false);

  const load = useCallback(() => {
    setState({});
    musicApi.albums().then((r) => setState(r.kind === 'ok' ? r.data : { failed: true }));
  }, []);

  useEffect(() => {
    document.title = 'Música · RTUB';
    load();
  }, [load]);

  const { albums, permissions: can } = state;

  const remove = async () => {
    if (!deleting) return;
    setDeleteState({ busy: true });
    const r = await musicApi.deleteAlbum(deleting.id);
    if (r.kind === 'ok') {
      setDeleting(null);
      setDeleteState({ busy: false });
      load();
    } else setDeleteState({ busy: false, error: 'Não foi possível eliminar o álbum.' });
  };

  return (
    <section className="page wrap music" aria-labelledby="music-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Discografia</p>
          <h1 id="music-title" className="page__title">
            Música
          </h1>
          <p className="page__lead">Os discos da RTUB, faixa a faixa: ouvir, ler a letra e ver os vídeos.</p>
        </div>
        {can && (can.canSeeStatistics || can.canManage) && (
          <div className="music__actions">
            {can.canSeeStatistics && (
              <button type="button" className="btn btn--ghost" onClick={() => setShowStats(true)}>
                <Icon name="chart" />
                Estatísticas
              </button>
            )}
            {can.canManage && (
              <button type="button" className="btn btn--primary" onClick={() => setEditing(null)}>
                <Icon name="plus" />
                Adicionar álbum
              </button>
            )}
          </div>
        )}
      </header>

      {state.failed ? (
        <div className="state" role="alert">
          <p className="state__title">Não foi possível carregar os álbuns.</p>
          <button type="button" className="btn btn--primary" onClick={load}>
            Tentar novamente
          </button>
        </div>
      ) : !albums ? (
        <Loading label="A carregar os álbuns…" />
      ) : albums.length === 0 ? (
        <div className="state">
          <Icon name="disc" className="state__icon" />
          <p className="state__title">Ainda não há álbuns.</p>
        </div>
      ) : (
        <ul className="albums">
          {albums.map((a) => (
            <li key={a.id} className="album-card">
              <a className="album-card__link" href={`/music/albums/${a.id}`}>
                {a.coverUrl ? (
                  <img className="album-card__cover" src={a.coverUrl} alt="" width="400" height="400" loading="lazy" />
                ) : (
                  <span className="album-card__cover album-card__cover--empty" aria-hidden="true">
                    <Icon name="disc" />
                  </span>
                )}
                <span className="album-card__body">
                  <span className="album-card__title">{a.title}</span>
                  <span className="album-card__meta">
                    {a.year && <span>{a.year}</span>}
                    <span>
                      {a.songCount} {a.songCount === 1 ? 'música' : 'músicas'}
                    </span>
                  </span>
                  {a.description && <span className="album-card__desc">{a.description}</span>}
                  {(a.isPrivate || a.isExclusive) && (
                    <span className="album-card__badges">
                      {a.isPrivate && <Badge icon="lock">Privado</Badge>}
                      {a.isExclusive && <Badge icon="star">Exclusivo</Badge>}
                    </span>
                  )}
                </span>
              </a>
              {can && (can.canManage || can.canDelete) && (
                <div className="album-card__manage">
                  {can.canManage && <IconButton icon="pencil" label={`Editar ${a.title}`} onClick={() => setEditing(a.id)} />}
                  {can.canDelete && <IconButton icon="trash" tone="danger" label={`Eliminar ${a.title}`} onClick={() => setDeleting(a)} />}
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      {editing !== undefined && can && (
        <AlbumForm
          albumId={editing}
          canManageExclusive={can.canManageExclusive}
          onClose={() => setEditing(undefined)}
          onSaved={() => {
            setEditing(undefined);
            load();
          }}
        />
      )}

      {deleting && (
        <ConfirmDialog
          title="Eliminar álbum"
          confirmLabel="Eliminar"
          busy={deleteState.busy}
          error={deleteState.error}
          onConfirm={remove}
          onClose={() => {
            setDeleting(null);
            setDeleteState({ busy: false });
          }}
        >
          <p>
            Eliminar o álbum <strong>{deleting.title}</strong>?
          </p>
          <p className="warning">
            <Icon name="warning" />
            As {deleting.songCount} músicas do álbum, os vídeos e o histórico de reproduções também são eliminados.
          </p>
        </ConfirmDialog>
      )}

      {showStats && can && <StatisticsDialog detailed={can.canSeeDetailedStatistics} onClose={() => setShowStats(false)} />}
    </section>
  );
}
