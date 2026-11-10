"""Audio + parsed chord sheet -> a play-along chart with a time for every chord.

Steps: split vocals (Demucs), force-align the known lyrics on the vocal track
(stable-ts / Whisper), track beats on the accompaniment, take each chord time
from its underlined syllable snapped to the nearest beat, and place the chords
of instrumental parts by matching chroma against the known chord sequence.

Writes into the output folder: chart.json (what the site stores), plus data.js
and a copy of the audio for review.html. The timing rules were tuned on two
songs; always review the result by ear before uploading it.

Usage: python align.py input/song.mp3 work/song.json out [whisper-model]
"""
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
os.environ.setdefault("TORCH_HOME", os.path.join(HERE, ".cache", "torch"))

import librosa
import numpy as np
import soundfile as sf
import torch

SPOKEN = {"0.0": "zero zero"}  # written form -> what is actually sung
NOTES = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
MAX_WORD = 1.5  # seconds; longest plausible sung word
FLATS = {"Db": "C#", "Eb": "D#", "Gb": "F#", "Ab": "G#", "Bb": "A#"}


def separate(audio_path, work):
    """Write vocals.wav and backing.wav once; reuse them on later runs."""
    vocals, backing = os.path.join(work, "vocals.wav"), os.path.join(work, "backing.wav")
    if os.path.exists(vocals) and os.path.exists(backing):
        return vocals, backing
    full = os.path.join(work, "full.wav")
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-i", audio_path,
                    "-ar", "44100", "-ac", "2", full], check=True)
    from demucs.apply import apply_model
    from demucs.pretrained import get_model
    model = get_model("htdemucs")
    data, rate = sf.read(full, dtype="float32")
    wav = torch.from_numpy(data.T)
    ref = wav.mean(0)
    wav = (wav - ref.mean()) / ref.std()
    with torch.no_grad():
        sources = apply_model(model, wav[None], device="cpu", split=True, progress=True)[0]
    sources = sources * ref.std() + ref.mean()
    index = model.sources.index("vocals")
    sf.write(vocals, sources[index].T.numpy(), rate)
    sf.write(backing, (sources.sum(0) - sources[index]).T.numpy(), rate)
    return vocals, backing


def align_words(vocals, lines, model_name, work):
    """Return (start, end) per lyric word, in sheet order."""
    cache = os.path.join(work, f"words-{model_name}.json")
    spoken = [[SPOKEN.get(w, w) for w in line["words"]] for line in lines]
    text = "\n".join(" ".join(words) for words in spoken)
    if os.path.exists(cache):
        timed = json.load(open(cache, encoding="utf-8"))
        if timed["text"] == text:
            return timed["spans"]
    import stable_whisper
    model = stable_whisper.load_model(model_name, device="cpu",
                                      download_root=os.path.join(HERE, ".cache", "whisper"))
    result = model.align(vocals, text, language="pt", original_split=True)
    pieces = [(w.word.strip().replace(" ", ""), w.start, w.end) for w in result.all_words()]
    spans, position = [], 0
    for words in spoken:
        for word in words:
            need, start, end = len(word.replace(" ", "")), None, None
            while need > 0 and position < len(pieces):
                piece, piece_start, piece_end = pieces[position]
                start = piece_start if start is None else start
                end = piece_end
                need -= len(piece)
                position += 1
            spans.append([start, end])
    assert position == len(pieces) and all(s[0] is not None for s in spans), \
        "aligned words do not match the sheet text"
    json.dump({"text": text, "spans": spans}, open(cache, "w", encoding="utf-8"))
    return spans


def template(chord):
    """12-bin pitch-class template for a chord name like Dm, A7, Bb."""
    chord = chord.split("/")[0]
    root = chord[:2] if len(chord) > 1 and chord[1] in "#b" else chord[:1]
    kind = chord[len(root):]
    base = NOTES.index(FLATS.get(root, root))
    minor = kind.startswith("m") and not kind.startswith("maj")
    steps = [0, 5, 7] if "sus4" in kind else [0, 3, 7] if minor else [0, 4, 7]
    if "maj7" in kind:
        steps.append(11)
    elif "7" in kind:
        steps.append(10)
    vector = np.zeros(12)
    vector[[(base + s) % 12 for s in steps]] = 1
    return vector / np.linalg.norm(vector)


def place_by_chroma(chords, chroma, low, high):
    """Best split of beats [low, high) into the given chord order. Returns start beats."""
    count, beats = len(chords), high - low
    if beats < count:
        return [low + min(i, max(beats - 1, 0)) for i in range(count)]
    score = np.array([[template(c) @ chroma[:, low + b] for b in range(beats)] for c in chords])
    best = np.full((count, beats), -np.inf)
    came = np.zeros((count, beats), dtype=int)  # 1 = this chord starts on this beat
    best[0, 0] = score[0, 0]
    for b in range(1, beats):
        for k in range(min(count, b + 1)):
            stay = best[k, b - 1]
            move = best[k - 1, b - 1] if k else -np.inf
            came[k, b] = move > stay
            best[k, b] = score[k, b] + max(stay, move)
    starts, k = [0] * count, count - 1
    for b in range(beats - 1, 0, -1):
        if came[k, b]:
            starts[k] = b
            k -= 1
    return [low + s for s in starts]


def main(audio_path, song_path, out_dir, model_name="medium"):
    work = os.path.dirname(os.path.abspath(song_path))
    song = json.load(open(song_path, encoding="utf-8"))
    vocals, backing = separate(audio_path, work)

    sung = [line for s in song["sections"] for line in s["lines"] if line["words"]]
    spans = iter(align_words(vocals, sung, model_name, work))
    for line in sung:
        # A word before an instrumental gap gets stretched across the gap; cap it.
        line["spans"] = [[start, min(end, start + MAX_WORD)] for start, end in
                         (next(spans) for _ in line["words"])]
        # Same drift the other way: a word placed after the gap. Lines are sung
        # without long pauses, so pull it back next to the word before it.
        for before, span in zip(line["spans"], line["spans"][1:]):
            if span[0] - before[1] > 1.0:
                span[:] = [before[1], before[1] + min(span[1] - span[0], 0.6)]

    # A section is sung in one go. A line left far before the rest of its section was
    # pulled across an instrumental part: move it next to the line that follows, at the
    # section's usual pace.
    for section in song["sections"]:
        lines_ = [line for line in section["lines"] if line["words"]]
        if len(lines_) < 3:
            continue
        pace = float(np.median([b["spans"][0][0] - a["spans"][0][0] for a, b in zip(lines_, lines_[1:])]))
        length = float(np.median([line["spans"][-1][1] - line["spans"][0][0] for line in lines_]))
        for line, following in reversed(list(zip(lines_, lines_[1:]))):
            start, end = line["spans"][0][0], line["spans"][-1][1]
            if following["spans"][0][0] - end > 2 * pace:
                new_start = following["spans"][0][0] - pace
                scale = length / max(end - start, 0.1)
                line["spans"] = [[new_start + (a - start) * scale, new_start + (b - start) * scale]
                                 for a, b in line["spans"]]

    y, rate = librosa.load(backing, sr=22050, mono=True)
    duration = len(y) / rate
    tempo, beat_frames = librosa.beat.beat_track(y=y, sr=rate)
    beats = librosa.frames_to_time(beat_frames, sr=rate)
    period = float(np.median(np.diff(beats)))
    harmonic = librosa.effects.harmonic(y)
    chroma = librosa.util.sync(librosa.feature.chroma_cqt(y=harmonic, sr=rate),
                               beat_frames, aggregate=np.median)[:, 1:]
    chroma = chroma / (np.linalg.norm(chroma, axis=0) + 1e-9)
    beat_at = lambda t: int(np.clip(np.searchsorted(beats, t + period / 2) - 1, 0, len(beats) - 1))

    def snap(t):
        nearest = beats[np.argmin(np.abs(beats - t))]
        return float(nearest) if abs(nearest - t) <= 0.4 * period else float(t)

    # Sung lines: chord time = underlined syllable, snapped to the beat.
    for line in sung:
        for chord in line["chords"]:
            start, end = line["spans"][chord["word"]]
            inside = chord["char"] / max(len(line["words"][chord["word"]]), 1)
            chord["raw"] = start + (end - start) * inside
            chord["time"] = snap(chord["raw"])
        line["start"], line["end"] = line["spans"][0][0], line["spans"][-1][1]

    # Is the recording in the sheet's key? Score sung chords against chroma at each shift.
    known = [(c["en"], beat_at(c["time"])) for line in sung for c in line["chords"]]
    shift_scores = [sum(np.roll(template(name), shift) @ chroma[:, beat:beat + 2].mean(1)
                        for name, beat in known) / len(known) for shift in range(12)]
    shift = int(np.argmax(shift_scores))
    chroma = np.roll(chroma, -shift, axis=0)

    # The sheet writes the interlude once; the recording may play it again. Where two
    # sung sections are far apart, assume that same interlude was played.
    written = next((sec for sec in song["sections"][1:-1] if sec["kind"] == "instrumental"), None)
    filled = []
    for section in song["sections"]:
        previous = filled[-1] if filled else None
        if written and previous and previous["kind"] != "instrumental"                 and section["kind"] != "instrumental"                 and section["lines"][0]["start"] - previous["lines"][-1]["end"] > 6 * period:
            filled.append(dict(json.loads(json.dumps(written)), inferred=True))
        filled.append(section)
    song["sections"] = filled

    # Instrumental parts: fit the known chord order to the beats between sung lines.
    lines = [line for s in song["sections"] for line in s["lines"]]
    index = 0
    while index < len(lines):
        if lines[index]["words"]:
            index += 1
            continue
        stop = index
        while stop < len(lines) and not lines[stop]["words"]:
            stop += 1
        t0 = lines[index - 1]["end"] if index else float(beats[0])
        t1 = lines[stop]["chords"][0]["time"] if stop < len(lines) and lines[stop]["chords"] \
            else lines[stop]["start"] if stop < len(lines) else duration
        low, high = beat_at(t0) + (1 if index else 0), max(beat_at(t1), beat_at(t0) + 1)
        chords = [c for line in lines[index:stop] for c in line["chords"]]
        runs = [c for i, c in enumerate(chords) if i == 0 or c["en"] != chords[i - 1]["en"]]
        starts = place_by_chroma([c["en"] for c in runs], chroma, low, min(high, chroma.shape[1]))
        starts.append(high)
        run = -1
        for i, chord in enumerate(chords):  # repeated chords share their run evenly
            if i == 0 or chord["en"] != chords[i - 1]["en"]:
                run, same = run + 1, [c for c in chords[i:]]
                size = next((n for n, c in enumerate(same) if c["en"] != chord["en"]), len(same))
                first = i
            beat = starts[run] + (starts[run + 1] - starts[run]) * (i - first) / size
            chord["time"] = float(beats[min(int(round(beat)), len(beats) - 1)])
        for n, line in enumerate(lines[index:stop]):
            line["start"] = line["chords"][0]["time"]
        for n, line in enumerate(lines[index:stop]):
            following = lines[index + n + 1]["start"] if index + n + 1 < len(lines) else duration
            line["end"] = following
        index = stop

    for section in song["sections"]:
        section["start"] = section["lines"][0]["start"]
        section["end"] = section["lines"][-1]["end"]
        for line in section["lines"]:
            line["wordTimes"] = [round(s[0], 2) for s in line.pop("spans", [])]
            line["start"], line["end"] = round(line["start"], 2), round(line["end"], 2)
            for chord in line["chords"]:
                chord["time"] = round(chord["time"], 2)
                chord.pop("raw", None)

    os.makedirs(out_dir, exist_ok=True)
    audio_name = "song" + os.path.splitext(audio_path)[1]
    shutil.copyfile(audio_path, os.path.join(out_dir, audio_name))
    shutil.copyfile(os.path.join(HERE, "review.html"), os.path.join(out_dir, "review.html"))
    song.update(audio=audio_name, duration=round(duration, 2),
                bpm=round(float(np.atleast_1d(tempo)[0]), 1), keyShift=shift,
                beats=[round(float(b), 2) for b in beats])
    with open(os.path.join(out_dir, "data.js"), "w", encoding="utf-8") as handle:
        handle.write("window.SONG = " + json.dumps(song, ensure_ascii=False) + ";\n")

    # The chart the site stores (format version 1, checked by PlayAlongChart.cs on upload).
    chart = {"version": 1, "duration": song["duration"], "bpm": song["bpm"], "beats": song["beats"],
             "sections": [{
                 "name": section["name"], "kind": section["kind"],
                 "start": round(section["start"], 2), "end": round(section["end"], 2),
                 "lines": [{
                     "text": line["text"], "start": line["start"], "end": line["end"],
                     "words": line["words"], "wordTimes": line["wordTimes"],
                     "chords": [{"label": c["pt"], "name": c["en"], "time": c["time"],
                                 "word": c.get("word"), "char": c.get("char")} for c in line["chords"]],
                 } for line in section["lines"]],
             } for section in song["sections"]]}
    with open(os.path.join(out_dir, "chart.json"), "w", encoding="utf-8") as handle:
        json.dump(chart, handle, ensure_ascii=False)

    with open(os.path.join(work, "align_review.txt"), "w", encoding="utf-8") as review:
        print(f"bpm {song['bpm']}  beats {len(beats)}  duration {duration:.1f}s  "
              f"key shift {shift} semitones (scores {np.round(shift_scores, 2).tolist()})",
              file=review)
        for section in song["sections"]:
            print(f"\n[{section['name']}] {section['start']:.1f}-{section['end']:.1f}", file=review)
            for line in section["lines"]:
                chords = "  ".join(f"{c['pt']}@{c['time']:.2f}" for c in line["chords"])
                print(f"  {line['start']:7.2f}-{line['end']:7.2f}  {line['text'][:44]:44}  {chords}",
                      file=review)
    times = [c["time"] for line in lines for c in line["chords"]]
    backwards = sum(b < a for a, b in zip(times, times[1:]))
    print(f"chords {len(times)}, out-of-order chord times {backwards}")


def demo():
    assert np.argmax(template("Dm")) in (2, 5, 9) and template("A7")[7] > 0  # A7 has G
    # 8 beats: 4 of F then 4 of C must split at beat 4.
    fake = np.stack([template("F")] * 4 + [template("C")] * 4, axis=1)
    assert place_by_chroma(["F", "C"], fake, 0, 8) == [0, 4]


if __name__ == "__main__":
    demo()
    main(*sys.argv[1:])
