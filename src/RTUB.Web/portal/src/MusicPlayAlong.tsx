import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { Loading } from './App';
import { Icon } from './icons';
import { loginTo, musicApi, type PlayAlong, type PlayAlongLine, type PlayAlongSection } from './musicApi';

/**
 * Aprender (task 094, named Tocar junto until 095, docs/react-music.md): play a song's chords along with its recording. The
 * strings run across the screen and each chord arrives at the line as a block of fret numbers, with
 * the lyric line underneath. Everything is driven by the audio clock: the chart (made offline,
 * tools/playalong) only says when each chord and word happens.
 */

const PX_PER_SECOND = 96;
/** Where "now" sits in the lane, as a share of its width. */
const NOW_AT = 0.24;

type Instrument = 'guitar' | 'bandolim' | 'cavaquinho' | 'bass';
const INSTRUMENTS: [Instrument, string][] = [
  ['guitar', 'Guitarra'],
  ['bandolim', 'Bandolim'],
  ['cavaquinho', 'Cavaquinho'],
  ['bass', 'Baixo'],
];

// Frets from the lowest string to the highest; -1 is a string that is not played.
// A chord that is not here still shows its name, never a guessed fingering.
const SHAPES: Record<Exclude<Instrument, 'bass'>, Record<string, number[]>> = {
  guitar: {
    A: [-1, 0, 2, 2, 2, 0], A7: [-1, 0, 2, 0, 2, 0], 'A#7': [-1, 1, 3, 1, 3, 1], Asus4: [-1, 0, 2, 2, 3, 0],
    B7: [-1, 2, 1, 2, 0, 2], Bb: [-1, 1, 3, 3, 3, 1], C: [-1, 3, 2, 0, 1, 0], C7: [-1, 3, 2, 3, 1, 0],
    Cmaj7: [-1, 3, 2, 0, 0, 0], 'C#m7': [-1, 4, 6, 4, 5, 4], D: [-1, -1, 0, 2, 3, 2], Dm: [-1, -1, 0, 2, 3, 1],
    Dm7: [-1, -1, 0, 2, 1, 1], E: [0, 2, 2, 1, 0, 0], Em: [0, 2, 2, 0, 0, 0], F: [1, 3, 3, 2, 1, 1],
    F7: [1, 3, 1, 2, 1, 1], G: [3, 2, 0, 0, 0, 3], G7: [3, 2, 0, 0, 0, 1], Gm: [3, 5, 5, 3, 3, 3],
    Am: [-1, 0, 2, 2, 1, 0], Am7: [-1, 0, 2, 0, 1, 0], B: [-1, 2, 4, 4, 4, 2], Bm: [-1, 2, 4, 4, 3, 2],
    Bm7: [-1, 2, 4, 2, 3, 2], Bb7: [-1, 1, 3, 1, 3, 1], 'C#7': [-1, 4, 3, 4, 2, -1], D7: [-1, -1, 0, 2, 1, 2],
    Dsus4: [-1, -1, 0, 2, 3, 3], E6: [0, 2, 2, 1, 2, 0], E7: [0, 2, 0, 1, 0, 0], Eb: [-1, 6, 8, 8, 8, 6],
    Em6: [0, 2, 2, 0, 2, 0], Em7: [0, 2, 0, 0, 0, 0], Emaj7: [0, 2, 1, 1, 0, 0], Esus4: [0, 2, 2, 2, 0, 0],
    'F#': [2, 4, 4, 3, 2, 2], 'F#m7': [2, 4, 2, 2, 2, 2], Fm: [1, 3, 3, 1, 1, 1], Fmaj7: [-1, -1, 3, 2, 1, 0],
    'G#m7': [4, 6, 4, 4, 4, 4], Gm7: [3, 5, 3, 3, 3, 3], Gmaj7: [3, 2, 0, 0, 0, 2],
  },
  bandolim: {
    A: [2, 2, 4, 5], A7: [2, 2, 4, 3], 'A#7': [3, 0, 1, 4], Asus4: [2, 0, 0, 0], B7: [4, 1, 0, 2], Bb: [3, 0, 1, 1],
    C: [0, 2, 3, 0], C7: [3, 2, 3, 0], Cmaj7: [4, 2, 3, 0], 'C#m7': [4, 2, 4, 0], D: [2, 0, 0, 2], Dm: [2, 0, 0, 1],
    Dm7: [2, 0, 3, 1], E: [1, 2, 2, 0], Em: [0, 2, 2, 0], F: [5, 3, 0, 1], F7: [5, 1, 0, 1], G: [0, 0, 2, 3],
    G7: [0, 0, 2, 1], Gm: [0, 0, 1, 3], Am: [2, 2, 3, 0], Am7: [2, 2, 3, 3], D7: [2, 0, 3, 2], E7: [1, 0, 2, 0],
  },
  // Tuned D G B D. C#m7 is shown as C#m.
  cavaquinho: {
    A: [2, 2, 2, 2], A7: [2, 2, 2, 5], 'A#7': [3, 3, 3, 6], Asus4: [2, 2, 3, 2], B7: [1, 2, 0, 1], Bb: [3, 3, 3, 3],
    C: [2, 0, 1, 2], C7: [2, 3, 1, 2], Cmaj7: [2, 4, 1, 2], 'C#m7': [2, 1, 2, 2], D: [0, 2, 3, 4], Dm: [0, 2, 3, 3],
    Dm7: [0, 2, 1, 3], E: [2, 1, 0, 2], Em: [2, 0, 0, 2], F: [3, 2, 1, 3], F7: [3, 2, 1, 1], G: [0, 0, 0, 0],
    G7: [0, 0, 0, 3], Gm: [5, 3, 3, 5], Am: [2, 2, 1, 2], Am7: [2, 2, 1, 5], D7: [0, 2, 1, 4], E7: [2, 1, 0, 0],
  },
};
const STRINGS: Record<Instrument, number> = { guitar: 6, bandolim: 4, cavaquinho: 4, bass: 4 };

/** The shape of a chord; a slash chord (Asus4/B) falls back to the chord without its bass note. */
const shapeOf = (name: string, instrument: Instrument) =>
  instrument === 'bass' ? bassNote(name) : (SHAPES[instrument][name] ?? SHAPES[instrument][name.split('/')[0]]);

/** The bass (E A D G) plays one note: the chord's root, or the note after the slash, at its lowest place. */
function bassNote(name: string) {
  const [, letter, accidental] = /^([A-G])([#b]?)/.exec(name.split('/').pop()!) ?? [];
  if (!letter) return undefined;
  const pitch = ('C D EF G A B'.indexOf(letter) + (accidental === '#' ? 1 : accidental === 'b' ? 11 : 0)) % 12;
  const string = pitch >= 4 && pitch <= 8 ? 0 : pitch === 2 || pitch === 3 ? 2 : 1;
  const shape = [-1, -1, -1, -1];
  shape[string] = (pitch - [4, 9, 2][string] + 12) % 12;
  return shape;
}

/**
 * Dedilhado (guitar): bass, 3rd string, 2nd and 1st together, 3rd string, then the same on the other
 * bass string. The bass strings follow the chord: its lowest string and the next one (MI: 5th and 4th;
 * a chord whose lowest string is the 4th goes down to the 5th). Rows are indexes into a shape.
 * ponytail: one pattern for every song; a pattern per song belongs in the chart when a second one exists.
 */
function pickingOf(name: string, shape: number[]) {
  const lowest = shape.findIndex((fret) => fret >= 0);
  const [first, second] = name.startsWith('E') ? [1, 2] : lowest >= 2 ? [2, 1] : [lowest, lowest + 1];
  return [[first], [3], [4, 5], [3], [second], [3], [4, 5], [3]];
}
/** The narrowest a picked note can be before the next one covers it. */
const PICK_PX = 24;

const clock = (seconds: number) => `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`;

const savedInstrument = (): Instrument => {
  try {
    const saved = localStorage.getItem('rtub-playalong-instrument');
    return INSTRUMENTS.find(([id]) => id === saved)?.[0] ?? 'guitar';
  } catch {
    return 'guitar';
  }
};

type Load =
  | { kind: 'loading' }
  | { kind: 'ready'; data: PlayAlong; audioUrl: string | null }
  | { kind: 'signin' | 'notfound' | 'failed' };

export default function MusicPlayAlong({ songId }: { songId: number }) {
  const [load, setLoad] = useState<Load>({ kind: 'loading' });

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
        <a className="back-link" href="/music">
          <Icon name="arrow" />
          Música
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

function Player({ data, audioUrl }: { data: PlayAlong; audioUrl: string | null }) {
  const { chart } = data;
  const lines = useMemo(() => chart.sections.flatMap((section) => section.lines.map((line) => ({ line, section }))), [chart]);
  const chords = useMemo(() => lines.flatMap((l) => l.line.chords), [lines]);

  const audio = useRef<HTMLAudioElement>(null);
  const lane = useRef<HTMLDivElement>(null);
  const track = useRef<HTMLDivElement>(null);
  const progress = useRef<HTMLDivElement>(null);
  const time = useRef<HTMLSpanElement>(null);
  const sung = useRef<HTMLParagraphElement>(null);
  const looped = useRef<PlayAlongSection | null>(null);

  const [playing, setPlaying] = useState(false);
  const [rate, setRate] = useState(1);
  const [loop, setLoop] = useState(false);
  const [instrument, setInstrument] = useState<Instrument>(savedInstrument);
  // Dedilhado is offered only where the Owner marked the song as played that way, and it opens in it.
  const [picking, setPicking] = useState(chart.picking);
  const [lineIndex, setLineIndex] = useState(0);
  const [chordIndex, setChordIndex] = useState(-1);
  const [stale, setStale] = useState(false);

  const current = lines[lineIndex];
  looped.current = loop ? (looped.current ?? current.section) : null;

  // One frame loop reads the audio clock and moves everything; React state only changes when the
  // line or the chord does.
  useEffect(() => {
    let frame = 0;
    const tick = () => {
      const el = audio.current;
      const t = el?.currentTime ?? 0;
      if (track.current && lane.current) {
        track.current.style.transform = `translate3d(${lane.current.clientWidth * NOW_AT - t * PX_PER_SECOND}px,0,0)`;
      }
      if (progress.current) progress.current.style.width = `${Math.min(t / chart.duration, 1) * 100}%`;
      if (time.current) time.current.textContent = `${clock(t)} / ${clock(chart.duration)}`;

      const line = lines.findIndex((l) => t < l.line.end);
      setLineIndex(line < 0 ? lines.length - 1 : line);
      let chord = -1;
      while (chord + 1 < chords.length && chords[chord + 1].time <= t) chord++;
      setChordIndex(chord);
      sung.current?.querySelectorAll<HTMLElement>('[data-t]').forEach((w) => w.classList.toggle('is-sung', Number(w.dataset.t) <= t));

      const section = looped.current;
      if (section && el && !el.paused && (t >= section.end || t < section.start - 1.5)) {
        el.currentTime = Math.max(section.start - 1, 0);
      }
      frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [chart, lines, chords]);

  // The screen stays on while the song plays (an instrument keeps both hands busy).
  useEffect(() => {
    if (!playing || !('wakeLock' in navigator)) return;
    let lock: WakeLockSentinel | null = null;
    navigator.wakeLock.request('screen').then((l) => (lock = l), () => undefined);
    return () => {
      void lock?.release();
    };
  }, [playing]);

  const toggle = () => {
    const el = audio.current;
    if (!el) return;
    if (el.paused) el.play().catch(() => undefined);
    else el.pause();
  };

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const tag = (e.target as HTMLElement).tagName;
      if (e.code !== 'Space' || tag === 'BUTTON' || tag === 'INPUT' || tag === 'A') return;
      e.preventDefault();
      toggle();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, []);

  const goTo = (section: PlayAlongSection) => {
    if (loop) looped.current = section;
    if (audio.current) audio.current.currentTime = Math.max(section.start - 1, 0);
  };

  const pickInstrument = (id: Instrument) => {
    setInstrument(id);
    try {
      localStorage.setItem('rtub-playalong-instrument', id);
    } catch {
      // Private mode: the choice lasts for this visit.
    }
  };

  const strings = STRINGS[instrument];

  return (
    <section className="page wrap playalong" aria-labelledby="playalong-title">
      <a className="back-link" href={`/music/albums/${data.albumId}`}>
        <Icon name="arrow" />
        {data.albumTitle}
      </a>
      <header className="playalong__head">
        <h1 id="playalong-title" className="playalong__title">
          {data.title}
        </h1>
        <p className="playalong__meta">Aprender, {Math.round(chart.bpm)} bpm</p>
        <button type="button" className="btn btn--primary playalong__play" onClick={toggle} disabled={!audioUrl}>
          <Icon name={playing ? 'pause' : 'playFill'} />
          {playing ? 'Pausa' : 'Tocar'}
        </button>
      </header>

      {!audioUrl && (
        <p className="form__banner" role="alert">
          O áudio desta música não está disponível. A pauta fica parada.
        </p>
      )}
      {stale && (
        <p className="form__banner" role="status">
          A gravação mudou desde que esta pauta foi feita. Os acordes podem estar fora do tempo.
        </p>
      )}

      <div className="playalong__lane" ref={lane} style={{ '--strings': strings } as CSSProperties} aria-hidden="true">
        {Array.from({ length: strings }, (_, row) => (
          <i key={row} className="playalong__string" style={{ '--row': row } as CSSProperties} />
        ))}
        <div className="playalong__track" ref={track}>
          {chart.beats.map((beat) => (
            <i key={beat} className="playalong__beat" style={{ left: beat * PX_PER_SECOND }} />
          ))}
          {chords.map((chord, i) => {
            const end = chords[i + 1]?.time ?? chart.duration;
            const shape = shapeOf(chord.name, instrument);
            const width = Math.max((end - chord.time) * PX_PER_SECOND - 4, 26);
            // A short chord gets the first half of the pattern, a very short one only its bass.
            const steps = picking && instrument === 'guitar' && shape
              ? pickingOf(chord.name, shape).slice(0, width >= 8 * PICK_PX ? 8 : width >= 4 * PICK_PX ? 4 : 1)
              : null;
            return (
              <div
                key={i}
                className={`playalong__block${i === chordIndex ? ' is-now' : i < chordIndex ? ' is-past' : ''}`}
                style={{ left: chord.time * PX_PER_SECOND, width }}
              >
                <b className="playalong__name">{chord.label}</b>
                {/* Lowest string on top, as the player sees the instrument (a tab is the other way up). */}
                {steps?.map((strings, step) =>
                  strings.map((string) =>
                    // RÉ mutes the 5th string in its chord box, but the thumb plays it open.
                    shape![string] < 0 && step % 4 !== 0 ? null : (
                      <span
                        key={`${step}-${string}`}
                        className={`playalong__fret${step % 4 === 0 ? ' playalong__fret--bass' : ''}`}
                        style={{ '--row': string, left: `${(step / steps.length) * 100}%` } as CSSProperties}
                      >
                        {Math.max(shape![string], 0)}
                      </span>
                    ),
                  ),
                )}
                {shape &&
                  !steps &&
                  shape.map((fret, row) =>
                    fret < 0 ? null : (
                      <span key={row} className="playalong__fret" style={{ '--row': row } as CSSProperties}>
                        {fret}
                      </span>
                    ),
                  )}
              </div>
            );
          })}
        </div>
        <i className="playalong__now" />
      </div>

      <div className="playalong__below">
        <div className="playalong__lyrics">
          <p className="playalong__section">{current.section.name}</p>
          <p className="playalong__line" ref={sung} key={lineIndex}>
            <Line line={current.line} timed />
          </p>
          <p className="playalong__line playalong__line--next">{lines[lineIndex + 1] && <Line line={lines[lineIndex + 1].line} />}</p>
        </div>
        <div className="playalong__shapes" aria-hidden="true">
          {[chords[Math.max(chordIndex, 0)], chords[Math.max(chordIndex, 0) + 1]].map((chord, i) => (
            <div key={i} className={`playalong__shape${i ? ' playalong__shape--next' : ''}`}>
              <small>{i ? 'A seguir' : 'Agora'}</small>
              <strong>{chord?.label ?? '–'}</strong>
              {chord && <Diagram frets={shapeOf(chord.name, instrument)} />}
            </div>
          ))}
        </div>
      </div>

      <p className="sr-only" aria-live="polite">
        {chordIndex >= 0 ? `Acorde ${chords[chordIndex].label}` : ''}
      </p>

      <nav className="playalong__timeline" aria-label="Secções da música">
        <div className="playalong__sections">
          {chart.sections.map((section, i) => {
            const from = i === 0 ? 0 : section.start;
            const to = chart.sections[i + 1]?.start ?? chart.duration;
            return (
              <button
                key={i}
                type="button"
                className={`playalong__part playalong__part--${section.kind}`}
                style={{ flexGrow: Math.max(to - from, 1) }}
                aria-current={section === current.section ? 'true' : undefined}
                onClick={() => goTo(section)}
                title={section.name}
              >
                <span>{section.name}</span>
              </button>
            );
          })}
        </div>
        <div className="playalong__progress">
          <div ref={progress} />
        </div>
      </nav>

      <div className="playalong__controls">
        <span className="playalong__time" ref={time}>
          0:00 / {clock(chart.duration)}
        </span>
        <div className="playalong__group" role="group" aria-label="Andamento">
          {[0.6, 0.8, 1].map((r) => (
            <button
              key={r}
              type="button"
              aria-pressed={rate === r}
              onClick={() => {
                setRate(r);
                if (audio.current) audio.current.playbackRate = r;
              }}
            >
              {r * 100}%
            </button>
          ))}
        </div>
        <div className="playalong__group" role="group" aria-label="Instrumento">
          {INSTRUMENTS.map(([id, label]) => (
            <button key={id} type="button" aria-pressed={instrument === id} onClick={() => pickInstrument(id)}>
              {label}
            </button>
          ))}
        </div>
        {chart.picking && instrument === 'guitar' && (
          <div className="playalong__group" role="group" aria-label="Mão direita">
            <button type="button" aria-pressed={!picking} onClick={() => setPicking(false)}>
              Acordes
            </button>
            <button type="button" aria-pressed={picking} onClick={() => setPicking(true)}>
              Dedilhado
            </button>
          </div>
        )}
        <div className="playalong__group">
          <button type="button" aria-pressed={loop} onClick={() => setLoop(!loop)}>
            Repetir secção
          </button>
        </div>
      </div>

      <audio
        ref={audio}
        src={audioUrl ?? undefined}
        preload="auto"
        onPlay={() => setPlaying(true)}
        onPause={() => setPlaying(false)}
        onLoadedMetadata={(e) => {
          e.currentTarget.playbackRate = rate;
          setStale(Math.abs(e.currentTarget.duration - chart.duration) > 2);
        }}
      />
    </section>
  );
}

/** A chord box: strings left to right from the lowest, five frets, the nut on top unless the shape sits higher. */
function Diagram({ frets }: { frets?: number[] }) {
  if (!frets) return null;
  const played = frets.filter((f) => f > 0);
  const base = Math.max(...played, 0) > 5 ? Math.min(...played) : 1;
  const x = (s: number) => 14 + (s * 100) / (frets.length - 1);
  return (
    <svg viewBox="0 0 128 122">
      {frets.map((_, s) => (
        <line key={s} x1={x(s)} y1="22" x2={x(s)} y2="117" />
      ))}
      {[0, 1, 2, 3, 4, 5].map((f) => (
        <line key={f} x1="14" y1={22 + f * 19} x2="114" y2={22 + f * 19} strokeWidth={f === 0 && base === 1 ? 4 : 1} />
      ))}
      {base > 1 && (
        <text x="122" y="36">
          {base}
        </text>
      )}
      {frets.map((f, s) =>
        f > 0 ? (
          <circle key={s} cx={x(s)} cy={22 + (f - base + 0.5) * 19} r="7" />
        ) : (
          <text key={s} x={x(s)} y="14">
            {f ? '×' : '○'}
          </text>
        ),
      )}
    </svg>
  );
}

/** A lyric line with each chord over the syllable it changes on, or the chords of an instrumental line. */
function Line({ line, timed = false }: { line: PlayAlongLine; timed?: boolean }) {
  if (line.words.length === 0) {
    return (
      <>
        {line.chords.map((chord, i) => (
          <span key={i} className="playalong__solo">
            {chord.label}
          </span>
        ))}
      </>
    );
  }

  return (
    <>
      {line.words.map((word, i) => {
        const marks = line.chords.filter((chord) => chord.word === i);
        const at = marks[0]?.char ?? 0;
        return (
          <span key={i} className="playalong__word" data-t={timed ? line.wordTimes[i] : undefined}>
            {marks.length > 0 && <b>{marks.map((chord) => chord.label).join(' ')}</b>}
            {marks.length > 0 ? (
              <>
                {word.slice(0, at)}
                <u>{word.slice(at)}</u>
              </>
            ) : (
              word
            )}{' '}
          </span>
        );
      })}
    </>
  );
}
