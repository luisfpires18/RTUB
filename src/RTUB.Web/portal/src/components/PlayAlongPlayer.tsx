// The play-along player: audio, scrolling chord lane and instrument chooser.
import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { Icon } from './icons';
import { type PlayAlong, type PlayAlongSection } from '../data/musicApi';
import { PX_PER_SECOND, NOW_AT, INSTRUMENTS, STRINGS, shapeOf, pickingOf, PICK_PX, clock, savedInstrument, type Instrument } from '../data/playAlongChords';
import { Diagram, Line } from './PlayAlongViews';

export function Player({ data, audioUrl }: { data: PlayAlong; audioUrl: string | null }) {
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
      <a className="back-link" href="/music/learn">
        <Icon name="arrow" />
        Aprender Músicas
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
