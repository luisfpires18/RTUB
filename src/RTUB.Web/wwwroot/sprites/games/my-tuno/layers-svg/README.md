# My-Tuno SVG layers (text-based)

These are text-based SVG replacements for the character customization layer stack.

## Why this folder exists

Some review surfaces cannot render diffs for binary PNG files (`Binary files are not supported`).
To keep art changes reviewable in PRs, these assets are authored as SVG text files.

## Regeneration

```bash
python scripts/generate_my_tuno_layers_svg.py
```

The generator outputs all 19 layers matching the same right-facing male cel-shaded template:

- body (1)
- eyes (1)
- hair (3)
- clothes (3)
- weapons (11)
