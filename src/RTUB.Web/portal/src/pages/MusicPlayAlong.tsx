import { useEffect, useState } from 'react';
import { Loading } from '../components/AppShared';
import { Icon } from '../components/icons';
import { musicApi, type PlayAlong } from '../data/musicApi';
import { loginTo } from '../data/content';
import { Player } from '../components/PlayAlongPlayer';

/**
 * Aprender (docs/react-music.md): play a song's chords along with its recording. The
 * strings run across the screen and each chord arrives at the line as a block of fret numbers, with
 * the lyric line underneath. Everything is driven by the audio clock: the chart (made offline,
 * tools/playalong) only says when each chord and word happens.
 */

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; data: PlayAlong; audioUrl: string | null }
  | { kind: 'signin' | 'notfound' | 'failed' };

export default function MusicPlayAlong({ songId }: { songId: number }) {
  const [load, setLoad] = useState<Load>({ kind: 'loading' });

  useEffect(() => {
    void musicApi.viewPlayAlong(songId);
  }, [songId]);

  useEffect(() => {
    let live = true;
    Promise.all([musicApi.playAlong(songId), musicApi.audio(songId)]).then(([chart, audio]) => {
      if (!live) return;
      if (chart.kind === 'ok') setLoad({ kind: 'ready', data: chart.data, audioUrl: audio.kind === 'ok' ? audio.data.audioUrl : null });
      else setLoad({ kind: chart.kind === 'signin' ? 'signin' : chart.kind === 'notfound' ? 'notfound' : 'failed' });
    });
    return () => {
      live = false;
    };
  }, [songId]);

  if (load.kind === 'loading') {
    return (
      <div className="wrap">
        <Loading label="A carregar a pauta…" />
      </div>
    );
  }

  if (load.kind !== 'ready') {
    return (
      <section className="page wrap" aria-labelledby="playalong-title">
        <a className="back-link" href="/music/learn">
          <Icon name="arrow" />
          Aprender Músicas
        </a>
        <div className="state">
          <Icon name={load.kind === 'signin' ? 'lock' : 'music'} className="state__icon" />
          <h1 id="playalong-title" className="state__title">
            {load.kind === 'signin'
              ? 'Aprender é para membros'
              : load.kind === 'notfound'
                ? 'Esta música ainda não tem pauta para aprender.'
                : 'Não foi possível carregar a pauta.'}
          </h1>
          {load.kind === 'signin' ? (
            <a className="btn btn--primary" href={loginTo(`/music/play/${songId}`)}>
              <Icon name="login" />
              Entrar como membro
            </a>
          ) : load.kind === 'failed' ? (
            <button type="button" className="btn btn--primary" onClick={() => location.reload()}>
              Tentar novamente
            </button>
          ) : null}
        </div>
      </section>
    );
  }

  return <Player data={load.data} audioUrl={load.audioUrl} />;
}
