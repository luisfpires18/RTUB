import { useCallback, useEffect, useState } from 'react';
import { Loading } from './App';
import { Icon } from './icons';
import { AlbumForm } from './MusicForms';
import { Badge, ConfirmDialog, Dialog, IconButton, Pager, plays } from './MusicUi';
import { loginTo, musicApi, type AlbumSummary, type MusicPermissions, type Statistics } from './musicApi';

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
      <header className="page__head music__head">
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

      {can && !can.isMember && albums && (
        <p className="note music__members">
          Há também álbuns reservados a membros. <a href={loginTo('/music')}>Entrar como membro</a>
        </p>
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

// ---------- statistics ----------

function StatisticsDialog({ detailed, onClose }: { detailed: boolean; onClose: () => void }) {
  const [stats, setStats] = useState<Statistics | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    musicApi.statistics().then((r) => (r.kind === 'ok' ? setStats(r.data) : setFailed(true)));
  }, []);

  return (
    <Dialog title="Estatísticas de música" size="lg" onClose={onClose}>
      {failed ? (
        <p className="form__banner" role="alert">
          Não foi possível carregar as estatísticas.
        </p>
      ) : !stats ? (
        <Loading label="A carregar as estatísticas…" />
      ) : (
        <div className="stats">
          <RankList
            title="Músicas mais tocadas"
            empty="Ainda não há músicas tocadas."
            label="músicas"
            rows={stats.songs.map((s) => ({ key: s.songId, title: s.title, sub: s.albumTitle, href: `/music/albums/${s.albumId}`, count: s.playCount }))}
          />
          <RankList
            title="Álbuns mais tocados"
            empty="Ainda não há álbuns tocados."
            label="álbuns"
            rows={stats.albums.map((a) => ({ key: a.albumId, title: a.title, sub: a.year ? String(a.year) : '', href: `/music/albums/${a.albumId}`, count: a.playCount }))}
          />
          {detailed && stats.members && <MemberStats members={stats.members} />}
        </div>
      )}
    </Dialog>
  );
}

type Row = { key: number; title: string; sub: string; href: string; count: number };

function RankList({ title, empty, label, rows }: { title: string; empty: string; label: string; rows: Row[] }) {
  const [page, setPage] = useState(1);
  const [size, setSize] = useState(5);
  const start = (page - 1) * size;

  return (
    <section className="stats__block">
      <h3 className="stats__title">{title}</h3>
      {rows.length === 0 ? (
        <p className="note">{empty}</p>
      ) : (
        <>
          <ol className="ranks" start={start + 1}>
            {rows.slice(start, start + size).map((r, i) => (
              <li key={r.key} className="rank">
                <span className={`rank__pos${start + i < 3 ? ' rank__pos--top' : ''}`} aria-hidden="true">
                  {start + i + 1}
                </span>
                <a className="rank__body" href={r.href}>
                  <span className="rank__title">{r.title}</span>
                  {r.sub && <span className="rank__sub">{r.sub}</span>}
                </a>
                <span className="rank__count">{plays(r.count)}</span>
              </li>
            ))}
          </ol>
          <Pager
            page={page}
            pageSize={size}
            total={rows.length}
            label={label}
            onPage={setPage}
            onPageSize={(n) => {
              setSize(n);
              setPage(1);
            }}
          />
        </>
      )}
    </section>
  );
}

function MemberStats({ members }: { members: NonNullable<Statistics['members']> }) {
  const [page, setPage] = useState(1);
  const [size, setSize] = useState(5);
  const start = (page - 1) * size;

  return (
    <section className="stats__block">
      <h3 className="stats__title">Por membro</h3>
      <p className="note">Visível apenas para a administração.</p>
      {members.length === 0 ? (
        <p className="note">Ainda não há reproduções de membros.</p>
      ) : (
        <>
          <ul className="member-stats">
            {members.slice(start, start + size).map((m) => (
              <li key={`${m.displayName}-${m.avatarUrl}`}>
                <details className="member-stats__item">
                  <summary>
                    <img src={m.avatarUrl} alt="" width="36" height="36" />
                    <span className="member-stats__who">
                      <span className="rank__title">{m.displayName}</span>
                      {m.fullName && m.fullName !== m.displayName && <span className="rank__sub">{m.fullName}</span>}
                    </span>
                    <span className="rank__count">{plays(m.totalPlays)}</span>
                    <Icon name="chevronDown" className="member-stats__chevron" />
                  </summary>
                  <ul className="member-stats__songs">
                    {m.songs.map((s) => (
                      <li key={`${s.albumTitle}-${s.songTitle}`}>
                        <span>
                          {s.songTitle} <span className="rank__sub">· {s.albumTitle}</span>
                        </span>
                        <span className="rank__count">{s.playCount}</span>
                      </li>
                    ))}
                  </ul>
                </details>
              </li>
            ))}
          </ul>
          <Pager
            page={page}
            pageSize={size}
            total={members.length}
            label="membros"
            onPage={setPage}
            onPageSize={(n) => {
              setSize(n);
              setPage(1);
            }}
          />
        </>
      )}
    </section>
  );
}
