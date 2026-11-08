#!/usr/bin/env python3
"""Builds the app's own Lottie animations (src/Lernkicker.Client/wwwroot/animations/*.json).

The animations are drawn here in code, like the SVG pictures, so they are ours to use and easy to change: run
`python3 tools/lottie/make_animations.py` after editing. They use only what the light Lottie player draws (shape layers,
paths, ellipses, rectangles, solid and gradient fills, strokes, transforms) and no expressions.

- sea: the moving water under a battleship board, a loop of waves, light patches and glints. It draws only light on a
  transparent background, so the board's own (themed) blue shows through.
- explosion, splash: a hit and a miss, played once on the cell.
- victory, defeat, draw: played once over any game's board when the game ends.
- sparkle, poof: a winning line or a promotion, and a captured piece in board games.
"""
import json
import math
import random
from pathlib import Path

OUT = Path(__file__).resolve().parents[2] / "src" / "Lernkicker.Client" / "wwwroot" / "animations"

LINEAR = ({"x": 0, "y": 0}, {"x": 1, "y": 1})
SMOOTH = ({"x": 0.42, "y": 0}, {"x": 0.58, "y": 1})
OUT_EASE = ({"x": 0.2, "y": 0.6}, {"x": 0.4, "y": 1})
IN_EASE = ({"x": 0.6, "y": 0}, {"x": 0.8, "y": 0.4})


def static(value):
    return {"a": 0, "k": value}


def anim(keys, ease=SMOOTH):
    """keys: [(frame, value), ...]; the last key only marks where the value ends."""
    frames = []
    for i, (t, v) in enumerate(keys):
        key = {"t": t, "s": v if isinstance(v, list) else [v]}
        if i < len(keys) - 1:
            e = ease[i] if isinstance(ease, list) else ease
            key["o"], key["i"] = e
        frames.append(key)
    return {"a": 1, "k": frames}


def prop(value):
    return value if isinstance(value, dict) else static(value)


def rgb(hex_color, alpha=1.0):
    h = hex_color.lstrip("#")
    return [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)] + [alpha]


def transform(p=(0, 0), a=(0, 0), s=(100, 100), r=0, o=100):
    return {"ty": "tr", "p": prop(list(p) if not isinstance(p, dict) else p), "a": static(list(a)),
            "s": prop(list(s) if not isinstance(s, dict) else s), "r": prop(r), "o": prop(o),
            "sk": static(0), "sa": static(0)}


def group(items, **tr):
    return {"ty": "gr", "it": items + [transform(**tr)], "nm": "g"}


def path(vertices, ins, outs, closed=False):
    return {"ty": "sh", "ks": static({"v": vertices, "i": ins, "o": outs, "c": closed})}


def polygon(points):
    return path([list(p) for p in points], [[0, 0]] * len(points), [[0, 0]] * len(points), True)


def ellipse(w, h, p=(0, 0)):
    return {"ty": "el", "p": static(list(p)), "s": prop([w, h] if not isinstance(w, dict) else w)}


def rect(w, h, p=(0, 0), r=0):
    return {"ty": "rc", "p": static(list(p)), "s": static([w, h]), "r": static(r)}


def fill(color, o=100):
    return {"ty": "fl", "c": static(color), "o": prop(o), "r": 1}


def stroke(color, w, o=100, dash=None):
    s = {"ty": "st", "c": static(color), "o": prop(o), "w": prop(w), "lc": 2, "lj": 2}
    if dash:
        s["d"] = [{"n": "d", "nm": "dash", "v": static(dash[0])}, {"n": "g", "nm": "gap", "v": static(dash[1])},
                  {"n": "o", "nm": "offset", "v": static(0)}]
    return s


def radial(stops, radius, alpha=None):
    """stops: [(position, hex)], alpha: [(position, opacity 0..1)] for a soft edge."""
    k = [x for pos, c in stops for x in [pos] + rgb(c)[:3]]
    if alpha:
        k += [x for pos, a in alpha for x in (pos, a)]
    return {"ty": "gf", "o": static(100), "r": 1, "t": 2, "s": static([0, 0]), "e": static([radius, 0]),
            "h": static(0), "a": static(0), "g": {"p": len(stops), "k": static(k)}}


def layer(index, name, shapes, op, p=(0, 0), s=(100, 100), r=0, o=100, ip=0):
    position = p if isinstance(p, dict) else static([p[0], p[1], 0])
    return {"ddd": 0, "ind": index, "ty": 4, "nm": name, "sr": 1, "ao": 0, "bm": 0, "ip": ip, "op": op, "st": 0,
            "ks": {"o": prop(o), "r": prop(r), "p": position, "a": static([0, 0, 0]),
                   "s": prop(list(s) + [100] if not isinstance(s, dict) else s)},
            "shapes": shapes}


def split_position(x, y):
    return {"s": True, "x": prop(x), "y": prop(y)}


def animation(name, w, h, op, layers, fr=30):
    # Lottie draws the first layer on top
    return {"v": "5.7.4", "fr": fr, "ip": 0, "op": op, "w": w, "h": h, "nm": name, "ddd": 0, "assets": [],
            "layers": [dict(l, ind=i + 1) for i, l in enumerate(layers)]}


def wave_path(length, amplitude, start, end):
    """A sine-like line from start to end: crests and troughs every half wave, joined by smooth curves."""
    half = length / 2
    handle = half * 0.36
    vertices, ins, outs = [], [], []
    x, up = start, True
    while x <= end + 0.1:
        vertices.append([round(x, 2), -amplitude if up else amplitude])
        ins.append([-handle, 0])
        outs.append([handle, 0])
        x += half
        up = not up
    return path(vertices, ins, outs)


def sea():
    random.seed(7)
    size, op = 400, 240
    layers = []
    # Glints: a short sparkle here and there
    for _ in range(12):
        x, y = random.uniform(20, 380), random.uniform(20, 380)
        t0 = random.randint(0, op - 30)
        sparkle = group([ellipse(14, 3), fill(rgb("#ffffff"))], s=anim([(t0, [0, 0]), (t0 + 8, [100, 100]), (t0 + 20, [0, 0])]),
                        p=(0, 0))
        layers.append(layer(0, "glint", [sparkle, group([ellipse(3, 10), fill(rgb("#ffffff"))],
                                                        s=anim([(t0, [0, 0]), (t0 + 8, [100, 100]), (t0 + 20, [0, 0])]))],
                            op, p=(x, y), o=90))
    # Waves: rows of broken crest lines, drifting by whole wave lengths per loop so the loop has no seam
    for row in range(13):
        y = 14 + row * 31 + random.uniform(-4, 4)
        length = random.choice([100, 125])
        direction = 1 if row % 2 == 0 else -1
        waves = random.choice([1, 2]) if row % 3 else 1
        amplitude = random.uniform(3, 5.5)
        dash = length / 2
        line = group([wave_path(length, amplitude, -2 * length * waves - 20, size + 2 * length * waves + 20),
                      stroke(rgb("#ffffff"), random.uniform(1.4, 2.4), o=random.uniform(30, 55), dash=(dash * 0.62, dash * 0.38))])
        x0 = 0 if direction > 0 else -length * waves
        bob = random.uniform(2, 4)
        layers.append(layer(0, f"wave{row}", [line], op,
                            p=split_position(anim([(0, x0), (op, x0 + direction * length * waves)], LINEAR),
                                             anim([(0, y), (op / 2, y + bob), (op, y)]))))
    # Light patches drifting slowly in circles, as if the sun shone through moving water
    for _ in range(7):
        cx, cy = random.uniform(40, 360), random.uniform(40, 360)
        rx, ry = random.uniform(10, 26), random.uniform(8, 20)
        phase = random.uniform(0, 2 * math.pi)
        keys_x = [(int(op * i / 4), cx + rx * math.cos(phase + i * math.pi / 2)) for i in range(5)]
        keys_y = [(int(op * i / 4), cy + ry * math.sin(phase + i * math.pi / 2)) for i in range(5)]
        w, h = random.uniform(150, 230), random.uniform(28, 46)
        patch = group([ellipse(w, h), fill(rgb("#ffffff"), random.uniform(3, 6))],
                      s=anim([(0, [100, 100]), (op / 2, [115, 90]), (op, [100, 100])]),
                      r=random.uniform(-25, 25))
        layers.append(layer(0, "light", [patch], op, p=split_position(anim(keys_x), anim(keys_y))))
    return animation("sea", size, size, op, layers)


def explosion():
    random.seed(3)
    size, op, c = 240, 32, 120
    layers = []
    # Debris flying out and falling
    for i in range(10):
        angle = random.uniform(-math.pi, 0) if i < 7 else random.uniform(0, math.pi)
        dist = random.uniform(55, 105)
        dx, dy = math.cos(angle) * dist, math.sin(angle) * dist
        piece = group([rect(random.uniform(5, 9), random.uniform(3, 6)), fill(rgb("#2b2f33"))],
                      r=anim([(0, 0), (op, random.choice([-1, 1]) * random.uniform(200, 500))], LINEAR))
        layers.append(layer(0, "debris", [piece], op,
                            p=split_position(anim([(0, c), (op - 4, c + dx)], OUT_EASE),
                                             anim([(0, c), (12, c + dy), (op - 4, c + dy + 40)], [OUT_EASE, IN_EASE])),
                            o=anim([(0, 100), (op - 8, 100), (op - 2, 0)])))
    # Sparks
    for _ in range(8):
        angle = random.uniform(0, 2 * math.pi)
        dist = random.uniform(70, 110)
        spark = group([ellipse(5, 5), fill(rgb("#ffe066"))])
        layers.append(layer(0, "spark", [spark], op,
                            p=split_position(anim([(0, c), (14, c + math.cos(angle) * dist)], OUT_EASE),
                                             anim([(0, c), (14, c + math.sin(angle) * dist)], OUT_EASE)),
                            o=anim([(0, 100), (10, 100), (16, 0)])))
    # Flash and shock ring
    layers.append(layer(0, "flash", [group([ellipse(120, 120), fill(rgb("#fffbe6"))])], op, p=(c, c),
                        s=anim([(0, [10, 10]), (4, [110, 110])], OUT_EASE), o=anim([(0, 100), (3, 100), (9, 0)])))
    layers.append(layer(0, "ring", [group([ellipse(200, 200), stroke(rgb("#ffffff"), 4)])], op, p=(c, c),
                        s=anim([(0, [10, 10]), (12, [105, 105])], OUT_EASE), o=anim([(0, 90), (12, 0)])))
    # Fireball: yellow core, orange, red edge, then dark smoke rising
    for color, d, size_ in [("#fff3bf", 0, 50), ("#ffd43b", 2, 80), ("#ff922b", 3, 110), ("#e03131", 4, 135)]:
        blobs = []
        for _ in range(3):
            ox, oy = random.uniform(-14, 14), random.uniform(-14, 8)
            blobs.append(group([ellipse(size_ * random.uniform(0.6, 0.85), size_ * random.uniform(0.6, 0.85)),
                                fill(rgb(color))], p=(ox, oy)))
        layers.append(layer(0, "fire", blobs, op, p=(c, c),
                            s=anim([(d, [0, 0]), (d + 7, [100, 100]), (22, [80, 80])], OUT_EASE),
                            o=anim([(0, 100), (14 - d, 100), (24 - d, 0)])))
    smoke = []
    for _ in range(5):
        smoke.append(group([ellipse(random.uniform(40, 60), random.uniform(40, 60)), fill(rgb("#495057"))],
                           p=(random.uniform(-30, 30), random.uniform(-20, 10))))
    layers.append(layer(0, "smoke", smoke, op, p=split_position(c, anim([(6, c), (op, c - 45)], OUT_EASE)),
                        s=anim([(6, [30, 30]), (op, [130, 130])], OUT_EASE), o=anim([(0, 0), (8, 70), (op - 2, 0)])))
    return animation("explosion", size, size, op, layers)


def splash():
    random.seed(5)
    size, op, c = 240, 34, 130
    layers = []
    for _ in range(9):
        dx = random.uniform(-60, 60)
        up = random.uniform(55, 100)
        drop = group([ellipse(6, 8), fill(rgb("#ffffff"))])
        layers.append(layer(0, "drop", [drop], op,
                            p=split_position(anim([(2, c), (op - 4, c + dx)], LINEAR),
                                             anim([(2, c), (13, c - up), (op - 4, c + 20)], [OUT_EASE, IN_EASE])),
                            o=anim([(0, 100), (op - 10, 100), (op - 4, 0)])))
    # The water column: tall, thin ellipses that shoot up and fall back
    for w, h, o, d in [(34, 120, 85, 0), (54, 70, 60, 2)]:
        column = group([ellipse(w, h, p=(0, -h / 2)), fill(rgb("#e7f5ff"))])
        layers.append(layer(0, "column", [column], op, p=(c, c),
                            s=anim([(d, [60, 0]), (d + 9, [100, 100]), (d + 22, [130, 0])], [OUT_EASE, IN_EASE]),
                            o=anim([(0, o), (d + 14, o), (d + 22, 0)])))
    for d in (0, 6):
        ring = group([ellipse(150, 60), stroke(rgb("#ffffff"), 3)])
        layers.append(layer(0, "ring", [ring], op, p=(c, c + 4),
                            s=anim([(d, [10, 10]), (d + 22, [110, 110])], OUT_EASE), o=anim([(d, 90), (d + 22, 0)])))
    return animation("splash", size, size, op, layers)


def confetti(op, start, count, colors, size):
    random.seed(11)
    layers = []
    for _ in range(count):
        x = random.uniform(10, size - 10)
        t0 = start + random.randint(0, 20)
        piece = group([rect(random.uniform(6, 10), random.uniform(10, 16)), fill(rgb(random.choice(colors)))],
                      s=anim([(t0, [100, 100]), (t0 + 10, [20, 100]), (t0 + 20, [100, 100]), (t0 + 30, [20, 100]),
                              (t0 + 40, [100, 100])]),
                      r=anim([(t0, random.uniform(0, 90)), (op, random.uniform(200, 600))], LINEAR))
        layers.append(layer(0, "confetti", [piece], op,
                            p=split_position(anim([(t0, x), (op, x + random.uniform(-40, 40))]),
                                             anim([(t0, -20), (op, size + 20)], LINEAR)),
                            o=anim([(0, 0), (t0, 0), (t0 + 1, 100), (op - 10, 100), (op, 0)])))
    return layers


def victory():
    size, op, c = 300, 100, 150
    gold, light, dark = rgb("#f5b301"), rgb("#ffe066"), rgb("#c47f00")
    layers = confetti(op, 4, 36, ["#e03131", "#1c7ed6", "#37b24d", "#f59f00", "#ae3ec9", "#ffffff"], size)
    pop = anim([(0, [0, 0]), (10, [118, 118]), (16, [95, 95]), (20, [100, 100]), (op - 14, [100, 100]), (op - 4, [0, 0])],
               [OUT_EASE, SMOOTH, SMOOTH, SMOOTH, IN_EASE])
    cup = [
        group([polygon([(-34, 52), (34, 52), (34, 62), (-34, 62)]), fill(dark)]),
        group([polygon([(-24, 40), (24, 40), (28, 52), (-28, 52)]), fill(gold)]),
        group([polygon([(-7, 14), (7, 14), (10, 40), (-10, 40)]), fill(dark)]),
        group([ellipse(30, 30, p=(-38, -30)), stroke(gold, 8)]),
        group([ellipse(30, 30, p=(38, -30)), stroke(gold, 8)]),
        group([path([[-42, -58], [42, -58], [0, 18]], [[0, 0], [0, 0], [40, 0]], [[0, 0], [0, 48], [-40, 0]], True), fill(gold)]),
        group([path([[-30, -54], [-18, -54], [-10, 4]], [[0, 0], [0, 0], [0, 0]], [[0, 0], [0, 30], [-18, -20]], True), fill(light)]),
        group([polygon([(-46, -64), (46, -64), (42, -56), (-42, -56)]), fill(dark)]),
        group([polygon([(0, -40), (5, -28), (18, -28), (8, -20), (12, -7), (0, -15), (-12, -7), (-8, -20), (-18, -28), (-5, -28)]),
               fill(rgb("#fff9db"))]),
    ]
    layers.append(layer(0, "trophy", cup, op, p=(c, c + 10), s=pop,
                        r=anim([(16, 0), (26, -6), (36, 6), (46, 0)])))
    rays = [group([polygon([(-7, -40), (7, -40), (0, -140)]), fill(rgb("#ffd43b"), 60)], r=i * 30) for i in range(12)]
    layers.append(layer(0, "rays", rays, op, p=(c, c), s=pop, r=anim([(0, 0), (op, 90)], LINEAR), o=70))
    return animation("victory", size, size, op, layers)


def defeat():
    size, op, c = 300, 100, 150
    layers = []
    random.seed(13)
    # Rain from a grey cloud
    for _ in range(14):
        x = random.uniform(80, 220)
        t0 = random.randint(10, 50)
        layers.append(layer(0, "rain", [group([rect(3, 12, r=1.5), fill(rgb("#74c0fc"))])], op,
                            p=split_position(x, anim([(t0, 95), (t0 + 18, 210)], LINEAR)),
                            o=anim([(0, 0), (t0, 0), (t0 + 1, 100), (t0 + 16, 100), (t0 + 18, 0)])))
    cloud = [group([ellipse(w, h, p=p), fill(rgb("#868e96"))]) for w, h, p in
             [(90, 60, (0, 0)), (70, 50, (-45, 12)), (70, 50, (45, 12)), (120, 34, (0, 22))]]
    layers.append(layer(0, "cloud", cloud, op, p=split_position(anim([(0, 120), (op, 180)]), 80),
                        o=anim([(0, 0), (8, 100), (op - 12, 100), (op - 2, 0)])))
    # A little boat rocking on the waves
    boat = [
        group([polygon([(-46, 0), (46, 0), (32, 22), (-32, 22)]), fill(rgb("#c92a2a"))]),
        group([rect(4, 60, p=(0, -30)), fill(rgb("#5c3d2e"))]),
        group([polygon([(4, -58), (4, -8), (38, -8)]), fill(rgb("#f8f9fa"))]),
        group([polygon([(-4, -50), (-4, -8), (-30, -8)]), fill(rgb("#e9ecef"))]),
    ]
    layers.append(layer(0, "boat", boat, op, p=split_position(c, anim([(0, 220), (25, 214), (50, 220), (75, 214), (op, 220)])),
                        r=anim([(0, -8), (25, 8), (50, -8), (75, 8), (op, -8)]),
                        o=anim([(0, 0), (8, 100), (op - 12, 100), (op - 2, 0)])))
    water = group([wave_path(60, 5, 0, 360), stroke(rgb("#1c7ed6"), 5)])
    layers.append(layer(0, "water", [water], op, p=split_position(anim([(0, -30), (op, 30)], LINEAR), 238),
                        o=anim([(0, 0), (8, 100), (op - 12, 100), (op - 2, 0)])))
    return animation("defeat", size, size, op, layers)


def star_shape(r_out, r_in, points=4):
    pts = []
    for i in range(points * 2):
        r = r_out if i % 2 == 0 else r_in
        a = math.pi * i / points - math.pi / 2
        pts.append((round(math.cos(a) * r, 2), round(math.sin(a) * r, 2)))
    return polygon(pts)


def sparkle():
    """Twinkling stars around a square or a disc: a winning line, a piece that became a king or queen."""
    random.seed(17)
    size, op, c = 200, 36, 100
    layers = []
    for i in range(9):
        angle = 2 * math.pi * i / 9 + random.uniform(-0.3, 0.3)
        dist = random.uniform(45, 80)
        t0 = random.randint(0, 10)
        r = random.uniform(8, 14)
        star = group([star_shape(r, r * 0.3), fill(rgb(random.choice(["#fff3bf", "#ffd43b", "#ffffff"])))],
                     s=anim([(t0, [0, 0]), (t0 + 8, [120, 120]), (t0 + 22, [0, 0])], [OUT_EASE, IN_EASE]),
                     r=anim([(t0, 0), (t0 + 22, 90)], LINEAR))
        layers.append(layer(0, "star", [star], op,
                            p=split_position(anim([(t0, c), (t0 + 22, c + math.cos(angle) * dist)], OUT_EASE),
                                             anim([(t0, c), (t0 + 22, c + math.sin(angle) * dist)], OUT_EASE))))
    layers.append(layer(0, "glow", [group([ellipse(90, 90), fill(rgb("#fff9db"))])], op, p=(c, c),
                        s=anim([(0, [20, 20]), (8, [100, 100])], OUT_EASE), o=anim([(0, 80), (16, 0)])))
    return animation("sparkle", size, size, op, layers)


def poof():
    """A puff of dust where a piece was taken."""
    random.seed(19)
    size, op, c = 200, 24, 100
    layers = []
    for i in range(8):
        angle = 2 * math.pi * i / 8 + random.uniform(-0.2, 0.2)
        dist = random.uniform(30, 55)
        puff = group([ellipse(random.uniform(26, 38), random.uniform(26, 38)), fill(rgb(random.choice(["#adb5bd", "#ced4da", "#868e96"])))])
        layers.append(layer(0, "puff", [puff], op,
                            p=split_position(anim([(0, c), (op - 2, c + math.cos(angle) * dist)], OUT_EASE),
                                             anim([(0, c), (op - 2, c + math.sin(angle) * dist - 8)], OUT_EASE)),
                            s=anim([(0, [40, 40]), (op - 2, [110, 110])], OUT_EASE), o=anim([(0, 90), (8, 90), (op - 2, 0)])))
    layers.append(layer(0, "ring", [group([ellipse(110, 110), stroke(rgb("#ffffff"), 3)])], op, p=(c, c),
                        s=anim([(0, [20, 20]), (12, [110, 110])], OUT_EASE), o=anim([(0, 80), (12, 0)])))
    return animation("poof", size, size, op, layers)


def draw():
    """Nobody won: two medals swing together and touch, with a few stars."""
    size, op, c = 300, 100, 150
    layers = confetti(op, 20, 14, ["#ffd43b", "#ffffff", "#74c0fc"], size)
    fade = anim([(0, 0), (6, 100), (op - 14, 100), (op - 4, 0)])

    def medal(color, ribbon, x0, x1, tilt):
        shapes = [
            group([polygon([(-14, -95), (0, -95), (8, -30), (-6, -30)]), fill(rgb(ribbon))]),
            group([polygon([(0, -95), (14, -95), (6, -30), (-8, -30)]), fill(rgb(ribbon), 80)]),
            group([ellipse(76, 76), fill(rgb("#c47f00"))]),
            group([ellipse(64, 64), fill(rgb(color))]),
            group([star_shape(22, 9, 5), fill(rgb("#fff9db"))]),
        ]
        return layer(0, "medal", shapes[::-1], op, p=split_position(anim([(0, x0), (22, x1)], OUT_EASE), c + 20),
                     r=anim([(0, tilt), (22, 0), (30, -tilt / 3), (38, 0)]), o=fade)

    layers.append(medal("#f5b301", "#1c7ed6", c - 120, c - 38, -25))
    layers.append(medal("#f5b301", "#e03131", c + 120, c + 38, 25))
    layers.append(layer(0, "touch", [group([star_shape(26, 8), fill(rgb("#ffffff"))])], op, p=(c, c + 10),
                        s=anim([(18, [0, 0]), (24, [130, 130]), (36, [0, 0])], [OUT_EASE, IN_EASE])))
    return animation("draw", size, size, op, layers)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for build in (sea, explosion, splash, victory, defeat, draw, sparkle, poof):
        data = build()
        (OUT / f"{data['nm']}.json").write_text(json.dumps(data, separators=(",", ":")), encoding="utf-8")
        print(data["nm"], len(data["layers"]), "layers")


if __name__ == "__main__":
    main()
