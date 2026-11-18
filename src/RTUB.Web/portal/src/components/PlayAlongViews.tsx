// Chord box diagram and chord-over-lyric line used by the play-along player.
import { type PlayAlongLine } from '../data/musicApi';

/** A chord box: strings left to right from the lowest, five frets, the nut on top unless the shape sits higher. */
export function Diagram({ frets }: { frets?: number[] }) {
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
export function Line({ line, timed = false }: { line: PlayAlongLine; timed?: boolean }) {
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
