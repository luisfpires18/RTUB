// Music statistics dialog: most played songs, albums and (for managers) members.
import { Banner } from './ui/Banner';
import { EmptyNote } from './ui/EmptyNote';
import { useEffect, useState } from 'react';
import { Icon } from './icons';
import { Dialog, plays } from './MusicUi';
import { musicApi, type Statistics } from '../data/musicApi';
import { Loading } from './ui/Loading';
import { TextPager } from './ui/Pager';

// ---------- statistics ----------

export function StatisticsDialog({ detailed, onClose }: { detailed: boolean; onClose: () => void }) {
  const [stats, setStats] = useState<Statistics | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    musicApi.statistics().then((r) => (r.kind === 'ok' ? setStats(r.data) : setFailed(true)));
  }, []);

  return (
    <Dialog title="Estatísticas de música" size="lg" onClose={onClose}>
      {failed ? (
        <Banner text="Não foi possível carregar as estatísticas." />
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
          <TextPager
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
        <EmptyNote>Ainda não há reproduções de membros.</EmptyNote>
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
          <TextPager
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
