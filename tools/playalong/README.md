# Play-along charts (Tocar junto)

Makes the chart that `/music/play/{songId}` plays (task 094, `docs/react-music.md` → *Tocar junto*):
the time of every chord, lyric word and beat of one recording. Runs on a PC, never on the server.

Inputs, per song: the songbook page as a PDF with a text layer (chords in colour above the lyrics,
the chord change underlined) and the recording the site plays for that song. Song files are never
committed: `input/`, `work/` and `out/` are git-ignored.

## Setup (once)

Python 3.13, ffmpeg on `PATH`, about 4 GB of disk for PyTorch and the models (downloaded on first
run into `.cache/` here).

```bash
python -m venv .venv
.venv/Scripts/python -m pip install torch torchaudio --index-url https://download.pytorch.org/whl/cpu
.venv/Scripts/python -m pip install -r requirements.txt
```

## One song

```bash
.venv/Scripts/python parse_sheet.py input/song.pdf work/song.json
```

Read `work/song.json.txt`: every line with its chords and the syllable each chord sits on. It must
match the sheet before going on. A sheet with a new chord spelling or layout needs the parser taught.

```bash
.venv/Scripts/python align.py input/song.mp3 work/song.json out
```

About five minutes on a CPU the first time (vocal split and alignment are cached in `work/`).
`work/align_review.txt` lists every line and chord with its time.

Open `out/review.html` in a browser and play along. Chord changes must land where you hear them:
this is the only real check, nothing here can listen. Then upload `out/chart.json` in the song's
edit form on the site (Tocar junto → Enviar pauta).

## What to look for in the review

- Sung lines: a chord every beat or two, at a steady pace. A line far from its neighbours was pulled
  across an instrumental part.
- Instrumental parts are placed by matching the audio against the written chord order: expect a
  chord or two a beat off.
- The sheet writes an interlude once; where the recording repeats it between sung sections the
  script inserts a copy.
- `key shift` other than 0 in `align_review.txt`: the recording is not in the sheet's key.

## Limits

Chords only (the sheets carry no melody). The timing rules were tuned on two songs. There is no
chart editor: to fix a chart, fix the cause here and upload again.
