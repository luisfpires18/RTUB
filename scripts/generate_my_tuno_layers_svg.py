#!/usr/bin/env python3
"""Generate My-Tuno layered SVG sprites inspired by classic LF2-style proportions.

Outputs 19 text-based SVG files (256x256) for body, eyes, hair, clothes and weapons.
"""

from pathlib import Path

ROOT = Path("src/RTUB.Web/wwwroot/sprites/games/my-tuno/layers-svg")
SCALE = 4
CANVAS = 64
PX = CANVAS * SCALE


def rgba(c):
    r, g, b, a = c
    return f"rgba({r},{g},{b},{a/255:.3f})"


def svg(rects):
    out = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{PX}" height="{PX}" viewBox="0 0 {PX} {PX}" shape-rendering="crispEdges">'
    ]
    for x, y, w, h, color in rects:
        out.append(
            f'<rect x="{x*SCALE}" y="{y*SCALE}" width="{w*SCALE}" height="{h*SCALE}" fill="{rgba(color)}"/>'
        )
    out.append("</svg>")
    return "\n".join(out) + "\n"


def write(rel, rects):
    path = ROOT / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(svg(rects), encoding="utf-8")


def block_outline(rects, x, y, w, h, color):
    rects += [
        (x, y, w, 1, color),
        (x, y + h - 1, w, 1, color),
        (x, y, 1, h, color),
        (x + w - 1, y, 1, h, color),
    ]


# Shared palette
OUTLINE = (35, 23, 20, 255)
SKIN = (220, 170, 132, 255)
SKIN_DARK = (182, 132, 96, 255)
SKIN_MID = (200, 150, 112, 255)

# BODY ----------------------------------------------------------------------
body = []
# Head (facing right)
body += [
    (29, 8, 10, 9, SKIN),
    (29, 8, 3, 9, SKIN_DARK),
    (36, 12, 2, 1, (40, 26, 24, 255)),   # eye slit
    (38, 13, 1, 1, (130, 84, 60, 255)),   # nose tip
]
block_outline(body, 29, 8, 10, 9, OUTLINE)

# Neck + torso skin base (clothes go on top as overlays)
body += [
    (30, 17, 2, 2, SKIN_MID),
    (30, 19, 10, 12, SKIN),
    (30, 19, 3, 12, SKIN_DARK),
]
block_outline(body, 30, 19, 10, 12, OUTLINE)

# Right arm (forward fighting pose)
body += [
    (40, 21, 6, 2, SKIN),
    (40, 21, 2, 2, SKIN_DARK),
    (45, 21, 2, 6, SKIN),
    (45, 21, 1, 6, SKIN_DARK),
    (45, 26, 2, 1, SKIN_MID),
]
block_outline(body, 40, 21, 6, 2, OUTLINE)
block_outline(body, 45, 21, 2, 6, OUTLINE)

# Left arm (behind torso)
body += [
    (28, 21, 2, 6, SKIN_DARK),
    (28, 26, 2, 1, SKIN_MID),
]
block_outline(body, 28, 21, 2, 6, OUTLINE)

# Legs + boots
PANTS = (64, 108, 176, 255)
PANTS_DARK = (42, 76, 136, 255)
BOOTS = (74, 46, 36, 255)
body += [
    (31, 31, 4, 11, PANTS),
    (31, 31, 1, 11, PANTS_DARK),
    (35, 31, 4, 11, PANTS),
    (35, 31, 1, 11, PANTS_DARK),
    (30, 42, 5, 2, BOOTS),
    (35, 42, 5, 2, BOOTS),
]
block_outline(body, 31, 31, 4, 11, OUTLINE)
block_outline(body, 35, 31, 4, 11, OUTLINE)
block_outline(body, 30, 42, 5, 2, OUTLINE)
block_outline(body, 35, 42, 5, 2, OUTLINE)
write("body/base.svg", body)

# EYES ----------------------------------------------------------------------
eyes = [
    (36, 12, 2, 1, (255, 255, 255, 230)),
    (37, 12, 1, 1, (32, 72, 140, 255)),
]
write("eyes/base.svg", eyes)

# HAIR ----------------------------------------------------------------------
hair_short = [
    (29, 7, 10, 2, (72, 44, 28, 255)),
    (30, 6, 7, 1, (108, 70, 44, 255)),
    (29, 9, 2, 2, (72, 44, 28, 255)),
]
hair_long = [
    (29, 7, 10, 2, (34, 34, 42, 255)),
    (30, 6, 7, 1, (70, 70, 88, 255)),
    (29, 9, 2, 10, (34, 34, 42, 255)),
    (37, 9, 2, 8, (34, 34, 42, 255)),
]
hair_spiky = [
    (29, 8, 10, 1, (110, 30, 30, 255)),
    (29, 6, 2, 2, (164, 54, 54, 255)),
    (32, 5, 2, 2, (164, 54, 54, 255)),
    (35, 6, 2, 2, (164, 54, 54, 255)),
    (38, 7, 1, 2, (164, 54, 54, 255)),
]
write("hair/short.svg", hair_short)
write("hair/long.svg", hair_long)
write("hair/spiky.svg", hair_spiky)

# CLOTHES (overlay-only) ----------------------------------------------------
armor = [
    (30, 19, 10, 12, (110, 124, 144, 235)),
    (33, 20, 5, 6, (168, 180, 200, 235)),
    (31, 24, 8, 1, (214, 184, 86, 245)),
    (31, 31, 8, 1, (214, 184, 86, 220)),
]
casual = [
    (30, 19, 10, 12, (72, 132, 208, 228)),
    (33, 20, 4, 5, (114, 176, 238, 220)),
    (30, 23, 10, 1, (88, 152, 220, 240)),
]
robe = [
    (30, 19, 10, 12, (98, 56, 136, 228)),
    (32, 20, 6, 5, (142, 92, 184, 220)),
    (33, 25, 4, 2, (235, 206, 126, 225)),
]
write("clothes/armor.svg", armor)
write("clothes/casual.svg", casual)
write("clothes/robe.svg", robe)

# WEAPONS -------------------------------------------------------------------
wood = (128, 88, 52, 255)
steel = (176, 184, 196, 255)
steel_dark = (98, 108, 126, 255)
gold = (188, 148, 72, 255)

write("weapons/sword_1h.svg", [
    (46, 24, 1, 7, steel), (46, 24, 1, 1, steel_dark), (45, 30, 3, 1, gold), (46, 31, 1, 3, wood)
])
write("weapons/sword_2h.svg", [
    (46, 19, 2, 12, steel), (46, 19, 2, 1, steel_dark), (45, 30, 4, 1, gold), (46, 31, 1, 6, wood)
])
write("weapons/dagger.svg", [
    (46, 26, 1, 4, steel), (45, 29, 3, 1, gold), (46, 30, 1, 2, wood)
])
write("weapons/axe_1h.svg", [
    (46, 23, 1, 10, wood), (46, 23, 3, 3, steel), (48, 24, 1, 2, steel_dark)
])
write("weapons/axe_2h.svg", [
    (46, 18, 1, 15, wood), (46, 19, 4, 4, steel), (49, 20, 1, 2, steel_dark)
])
write("weapons/bow.svg", [
    (45, 20, 1, 14, wood), (46, 22, 1, 1, wood), (47, 24, 1, 1, wood), (48, 26, 1, 1, wood),
    (46, 32, 1, 1, wood), (47, 30, 1, 1, wood), (48, 28, 1, 1, wood), (46, 20, 1, 14, (210, 210, 220, 220))
])
write("weapons/hammer.svg", [
    (46, 23, 1, 11, wood), (44, 22, 5, 3, steel_dark), (45, 23, 3, 1, steel)
])
write("weapons/mace.svg", [
    (46, 23, 1, 10, wood), (45, 20, 3, 4, steel), (45, 20, 1, 1, steel_dark), (47, 22, 1, 1, steel_dark)
])
write("weapons/shield.svg", [
    (43, 22, 5, 7, (118, 76, 46, 255)), (44, 23, 3, 5, (156, 102, 64, 255)), (45, 24, 1, 1, gold)
])
write("weapons/spear.svg", [
    (46, 18, 1, 16, wood), (47, 26, 1, 8, wood), (45, 17, 3, 2, steel)
])
write("weapons/staff.svg", [
    (46, 18, 1, 16, wood), (47, 26, 1, 8, wood), (45, 16, 3, 2, (96, 212, 236, 255)), (46, 16, 1, 1, (176, 246, 255, 255))
])

print(f"Generated LF2-style SVG layers at {ROOT}")
