// Chord shapes, picking and timing constants for the play-along lane.

export const PX_PER_SECOND = 96;
/** Where "now" sits in the lane, as a share of its width. */
export const NOW_AT = 0.24;

export type Instrument = 'guitar' | 'bandolim' | 'cavaquinho' | 'bass';
export const INSTRUMENTS: [Instrument, string][] = [
  ['guitar', 'Guitarra'],
  ['bandolim', 'Bandolim'],
  ['cavaquinho', 'Cavaquinho'],
  ['bass', 'Baixo'],
];

// Frets from the lowest string to the highest; -1 is a string that is not played.
// A chord that is not here still shows its name, never a guessed fingering.
export const SHAPES: Record<Exclude<Instrument, 'bass'>, Record<string, number[]>> = {
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
export const STRINGS: Record<Instrument, number> = { guitar: 6, bandolim: 4, cavaquinho: 4, bass: 4 };

/** The shape of a chord; a slash chord (Asus4/B) falls back to the chord without its bass note. */
export const shapeOf = (name: string, instrument: Instrument) =>
  instrument === 'bass' ? bassNote(name) : (SHAPES[instrument][name] ?? SHAPES[instrument][name.split('/')[0]]);

/** The bass (E A D G) plays one note: the chord's root, or the note after the slash, at its lowest place. */
export function bassNote(name: string) {
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
export function pickingOf(name: string, shape: number[]) {
  const lowest = shape.findIndex((fret) => fret >= 0);
  const [first, second] = name.startsWith('E') ? [1, 2] : lowest >= 2 ? [2, 1] : [lowest, lowest + 1];
  return [[first], [3], [4, 5], [3], [second], [3], [4, 5], [3]];
}
/** The narrowest a picked note can be before the next one covers it. */
export const PICK_PX = 24;

export const clock = (seconds: number) => `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`;

export const savedInstrument = (): Instrument => {
  try {
    const saved = localStorage.getItem('rtub-playalong-instrument');
    return INSTRUMENTS.find(([id]) => id === saved)?.[0] ?? 'guitar';
  } catch {
    return 'guitar';
  }
};
