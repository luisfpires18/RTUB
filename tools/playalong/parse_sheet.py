"""Chord sheet PDF (RTUB cancioneiro layout) -> work/song.json.

Chords are the small purple words, lyrics the 9pt black words, and the
underline under a syllable marks where the chord above it changes.

Usage: python parse_sheet.py input/song.pdf work/song.json
"""
import json
import re
import sys
import unicodedata

import pdfplumber

ROOTS = {"DÓ": "C", "RÉ": "D", "MI": "E", "FÁ": "F", "SOL": "G", "LÁ": "A", "SI": "B"}


QUALITY = {"M": "", "7M": "maj7", "M7": "maj7", "4": "sus4"}


def to_english(chord):
    """FÁ -> F, Rem7 -> Dm7, DO7M -> Cmaj7, DOM -> C, (LA4/SI) -> Asus4/B."""
    chord = chord.strip("()")
    if "/" in chord:
        return "/".join(to_english(part) for part in chord.split("/"))
    plain = unicodedata.normalize("NFD", chord).encode("ascii", "ignore").decode()
    for pt in sorted(ROOTS, key=len, reverse=True):
        root = unicodedata.normalize("NFD", pt).encode("ascii", "ignore").decode()
        if plain.upper().startswith(root):
            rest = plain[len(root):]
            accidental = rest[:1] if rest[:1] in ("#", "b") else ""
            quality = rest[len(accidental):]
            return ROOTS[pt] + accidental + QUALITY.get(quality, quality)
    raise ValueError(f"unknown chord {chord!r}")


def rows(words, tolerance=2.5):
    """Group words that share a baseline, top to bottom, left to right."""
    out = []
    for word in sorted(words, key=lambda w: (w["top"], w["x0"])):
        if out and abs(out[-1][0]["top"] - word["top"]) <= tolerance:
            out[-1].append(word)
        else:
            out.append([word])
    return [sorted(row, key=lambda w: w["x0"]) for row in out]


def split_columns(row, gap=15):
    parts = [[row[0]]]
    for word in row[1:]:
        if word["x0"] - parts[-1][-1]["x1"] > gap:
            parts.append([word])
        else:
            parts[-1].append(word)
    return parts


def parse(pdf_path):
    page = pdfplumber.open(pdf_path).pages[0]
    words = page.extract_words(extra_attrs=["fontname", "size", "non_stroking_color"])
    is_chord = lambda w: len(w["non_stroking_color"]) == 3
    bottom_limit = max(w["top"] for w in words if is_chord(w)) + 25
    plain_sizes = [round(w["size"], 1) for w in words
                   if not is_chord(w) and "Bold" not in w["fontname"]]
    lyric_size = max(set(plain_sizes), key=plain_sizes.count)  # sheets use 9pt or 10.5pt
    is_lyric = lambda w: (not is_chord(w) and lyric_size - 2 < w["size"] < lyric_size + 0.6
                          and w["top"] < bottom_limit
                          and "Bold" not in w["fontname"])
    is_marker = lambda w: not is_chord(w) and "Bold" in w["fontname"] and w["size"] > 8.5

    chord_words = [w for w in words if is_chord(w)]
    top_limit = min(w["top"] for w in chord_words) - 5  # skip title block
    lyric_parts = [part for row in rows([w for w in words if is_lyric(w)])
                   for part in split_columns(row)]
    starts = sorted({round(part[0]["x0"]) for part in lyric_parts})
    # ponytail: two-column sheets only; widest gap between line starts is the gutter
    gutter = max(zip(starts, starts[1:]), key=lambda p: p[1] - p[0], default=(0, 10 ** 6))
    boundary = gutter[1] - 5 if gutter[1] - gutter[0] > 80 else 10 ** 6
    underlines = [r for r in page.rects if r["height"] < 1 and r["width"] < 60]

    columns = [[], []]
    column_of = lambda x: 0 if x < boundary else 1

    for part in lyric_parts:
        if part[0]["top"] < top_limit:
            continue
        columns[column_of(part[0]["x0"])].append(("lyric", part[0]["top"], part))
    for row in rows(chord_words):
        for side in (0, 1):
            part = [w for w in row if column_of(w["x0"]) == side]
            if part:
                columns[side].append(("chords", part[0]["top"], part))
    for row in rows([w for w in words if is_marker(w) and w["top"] > top_limit]):
        for part in split_columns(row, gap=40):
            text = " ".join(w["text"] for w in part)
            if "REFR" in text.upper():
                kind = "refrain-def" if "--" in text else "refrain-ref"
                columns[column_of(part[0]["x0"])].append((kind, part[0]["top"], part))

    def lyric_line(part, chords):
        x0, x1, top = part[0]["x0"], part[-1]["x1"], part[0]["top"]
        marks = sorted((u for u in underlines
                        if 0.5 * lyric_size < u["top"] - top < 1.25 * lyric_size and x0 - 2 <= u["x0"] <= x1 + 2),
                       key=lambda u: u["x0"])
        line = {"text": " ".join(w["text"] for w in part),
                "words": [w["text"] for w in part], "chords": []}
        for index, chord in enumerate(chords):
            if len(marks) == len(chords):
                x = marks[index]["x0"]
            elif marks:
                x = min(marks, key=lambda u: abs(u["x0"] - chord["x0"]))["x0"]
            else:
                x = chord["x0"]
            word = max((i for i, w in enumerate(part) if w["x0"] <= x + 1.5), default=0)
            target = part[word]
            share = (x - target["x0"]) / max(target["x1"] - target["x0"], 1)
            char = min(len(target["text"]) - 1, max(0, round(share * len(target["text"]))))
            line["chords"].append({"pt": chord["text"], "en": to_english(chord["text"]),
                                   "word": word, "char": char})
        return line

    sections = []
    refrain = None

    def open_section(name, kind):
        sections.append({"name": name, "kind": kind, "lines": []})
        return sections[-1]

    current = None
    verse_count = 0
    for column in columns:
        items = sorted(column, key=lambda item: item[1])
        index = 0
        while index < len(items):
            kind, top, part = items[index]
            following = items[index + 1] if index + 1 < len(items) else None
            if kind == "chords":
                names = [w for w in part if w["text"] != "/"]
                if following and following[0] == "lyric" and 0 < following[1] - top < 12 \
                        and not any(w["text"] == "/" for w in part):
                    if current is None:
                        verse_count += 1
                        current = open_section(f"Verso {verse_count}", "verse")
                    current["lines"].append(lyric_line(following[2], names))
                    index += 2
                    continue
                if not sections or sections[-1]["kind"] != "instrumental" or current is not None:
                    open_section("Instrumental", "instrumental")
                current = None
                sections[-1]["lines"].append({
                    "text": "", "words": [],
                    "chords": [{"pt": w["text"], "en": to_english(w["text"])} for w in names]})
            elif kind == "lyric":  # lyric line with no chord row of its own
                if current is None:
                    verse_count += 1
                    current = open_section(f"Verso {verse_count}", "verse")
                current["lines"].append(lyric_line(part, []))
            elif kind == "refrain-def":
                current = refrain = open_section("Refrão", "refrain")
            else:
                copy = open_section("Refrão", "refrain")
                copy["lines"] = json.loads(json.dumps(refrain["lines"])) if refrain else []
                current = None
            index += 1
        current = None

    instrumental = [s for s in sections if s["kind"] == "instrumental"]
    for number, section in enumerate(instrumental):
        section["name"] = ("Introdução" if section is sections[0]
                           else "Final" if section is sections[-1] else "Interlúdio")
    title = max((w for w in words if w["top"] < top_limit), key=lambda w: w["size"])
    title_text = " ".join(w["text"] for w in words
                          if abs(w["top"] - title["top"]) < 2 and w["size"] == title["size"])
    return {"title": title_text, "sections": sections}


if __name__ == "__main__":
    song = parse(sys.argv[1])
    with open(sys.argv[2], "w", encoding="utf-8") as handle:
        json.dump(song, handle, ensure_ascii=False, indent=1)
    out = open(sys.argv[2] + ".txt", "w", encoding="utf-8")
    for section in song["sections"]:
        print(f"[{section['name']}]", file=out)
        for line in section["lines"]:
            marks = " ".join(
                f"{c['pt']}@{line['words'][c['word']][c['char']:]}" if "word" in c else c["pt"]
                for c in line["chords"])
            print(f"  {line['text']}   <- {marks}", file=out)
    assert [to_english(c) for c in ("FÁ", "Rem7", "DO7M", "DOM", "(LA4/SI)", "SIb", "Mim")] ==         ["F", "Dm7", "Cmaj7", "C", "Asus4/B", "Bb", "Em"]
    assert song["sections"] and all(s["lines"] for s in song["sections"]), "empty section"
