import { useCallback, useEffect, useState } from 'react';
import { Loading } from './App';
import { Icon } from './icons';
import { musicApi, type LearnSong } from './musicApi';

/**
 * /music/learn - Aprender Músicas (task 095), members only: every song that has an Aprender chart, in
 * the same grid as /music. A card opens the song's Aprender page and nothing else.
 */
export default function MusicLearn() {
  const [state, setState] = useState<{ songs?: LearnSong[]; failed?: boolean }>({});

  const load = useCallback(() => {
    setState({});
    musicApi.learnSongs().then((r) => setState(r.kind === 'ok' ? { songs: r.data } : { failed: true }));
  }, []);

  useEffect(() => {
    document.title = 'Aprender Músicas · RTUB';
    load();
  }, [load]);

  const { songs } = state;

  return (
    <section className="page wrap music" aria-labelledby="learn-title">
      <header className="page__head events-page__head">
        <div>
          <p className="eyebrow">Música</p>
          <h1 id="learn-title" className="page__title">
            Aprender Músicas
          </h1>
          <p className="page__lead">As músicas com pauta para aprender: os acordes a acompanhar a gravação.</p>
        </div>
      </header>

      {state.failed ? (
        <div className="state" role="alert">
          <p className="state__title">Não foi possível carregar as músicas.</p>
          <button type="button" className="btn btn--primary" onClick={load}>
            Tentar novamente
          </button>
        </div>
      ) : !songs ? (
        <Loading label="A carregar as músicas…" />
      ) : songs.length === 0 ? (
        <div className="state">
          <Icon name="disc" className="state__icon" />
          <p className="state__title">Ainda não há músicas para aprender.</p>
        </div>
      ) : (
        <ul className="albums">
          {songs.map((s) => (
            <li key={s.songId} className="album-card">
              <a className="album-card__link" href={`/music/play/${s.songId}`}>
                {s.coverUrl ? (
                  <img className="album-card__cover" src={s.coverUrl} alt="" width="400" height="400" loading="lazy" />
                ) : (
                  <span className="album-card__cover album-card__cover--empty" aria-hidden="true">
                    <Icon name="disc" />
                  </span>
                )}
                <span className="album-card__body">
                  <span className="album-card__title">{s.title}</span>
                  <span className="album-card__meta">
                    <span>{s.albumTitle}</span>
                    {s.year && <span>{s.year}</span>}
                  </span>
                </span>
              </a>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
