# My-Tuno SVG layers (text-based)

These are text-based SVG layers for the character customization stack, now redrawn in a compact **LF2-inspired side-fighter silhouette** (right-facing male base).

## Why this folder exists

Some review surfaces cannot render binary PNG diffs (`Binary files are not supported`).
These assets stay reviewable because every layer is plain text SVG.

## Regeneration

```bash
python scripts/generate_my_tuno_layers_svg.py
```

The generator writes all 19 layers aligned to the same body template:

- body (1)
- eyes (1)
- hair (3)
- clothes (3)
- weapons (11)
