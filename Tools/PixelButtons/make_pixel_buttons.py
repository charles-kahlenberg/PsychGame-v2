"""Draws the Group 2 pixel-art buttons (Feature.PixelButtons) into
Assets/Resources/PixelButtons.

Submit and Refresh are drawn like the card backs: a black outline with
stepped corners, a grainy frame, a black inner line with little drips, and a
noisy fill that darkens toward the bottom. Refresh carries its uses left as
diamonds in a tab on top, where the cards have their numerals. The menu button
is the same plate with a back arrow and "MENU".

Each button is drawn at 1 pixel = 1 unit of the game's 800x450 canvas, then
saved 4x larger (nearest neighbour) so the pixels stay even when the canvas is
scaled to the screen; PixelButton sizes them back down. Every state is its own
image, with room for the drop shadow and the 1-pixel lift on hover.

Run from anywhere with Python 3 + numpy + Pillow:
    python Tools/PixelButtons/make_pixel_buttons.py
"""
import os
import zlib

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Resources', 'PixelButtons')
SCALE = 4  # PixelButton.TexelsPerUnit
DEPTH = 2  # how far the drop shadow sits below the plate


def hx(s, a=255):
    s = s.lstrip('#')
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)


def seed_of(name):
    return zlib.crc32(name.encode())


# ------------------------------------------------------------ pixel font (5x7, bolded)
FONT = {
    'B': ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
    'E': ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    'F': ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
    'H': ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    'I': ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    'M': ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    'N': ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"],
    'R': ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    'S': [".###.", "#...#", "#....", ".###.", "....#", "#...#", ".###."],
    'T': ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    'U': ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
}
# Bolding by thickening every stroke closes up M's middle, so it's drawn bold.
BOLD = {
    'M': ["##...##", "###.###", "##.#.##", "##.#.##", "##...##", "##...##", "##...##"],
}


def sprite(rows):
    return np.array([[c == '#' for c in r] for r in rows], dtype=bool)


def glyph(ch):
    if ch in BOLD:
        return sprite(BOLD[ch])
    g = sprite(FONT[ch])
    b = np.zeros((g.shape[0], g.shape[1] + 1), dtype=bool)
    b[:, :-1] |= g
    b[:, 1:] |= g
    return b


def text_mask(s):
    gs = [glyph(c) for c in s]
    w = sum(g.shape[1] for g in gs) + len(gs) - 1
    m = np.zeros((7, w), dtype=bool)
    x = 0
    for g in gs:
        m[:, x:x + g.shape[1]] = g
        x += g.shape[1] + 1
    return m


PIP_FULL = sprite(["..#..", ".###.", "#####", ".###.", "..#.."])
PIP_EMPTY = sprite(["..#..", ".#.#.", "#...#", ".#.#.", "..#.."])
ICON_BACK = sprite(["...#...", "..##...", ".######", "#######", ".######", "..##...", "...#..."])


def stamp(img, mask, x, y, col):
    h, w = mask.shape
    img[y:y + h, x:x + w][mask] = col


# ------------------------------------------------------------ palettes (sampled from the card backs)
CARD = {
    'green': dict(frame='#2a452c', shade='#061c08', hi='#3edb4b', base='#2caa37', mid='#1a7d23', dark='#123915'),
    'teal':  dict(frame='#435055', shade='#1d2224', hi='#6ca9b2', base='#5c8ba2', mid='#496e71', dark='#304955'),
    # not a card colour: the background's slate, for the menu button
    'slate': dict(frame='#2b2f3f', shade='#151821', hi='#7a84b4', base='#5b6282', mid='#464c68', dark='#2e3246'),
}
BLACK = (0, 0, 0, 255)
CREAM = hx('#F7F1E6')
DIM_TEXT = hx('#b9b9c2')
DIM_SHADOW = hx('#2a2a30')
EMPTY_PIP = hx('#8a8f99')


def shift(pal, step):
    """Hover is a step lighter, pressed a step darker."""
    order = ['dark', 'mid', 'base', 'hi']
    out = dict(pal)
    for i, k in enumerate(order):
        out[k] = pal[order[min(max(i + step, 0), 3)]]
    if step > 0:
        r, g, b, _ = hx(pal['hi'])
        out['hi'] = '#%02x%02x%02x' % tuple(int(v + (255 - v) * 0.22) for v in (r, g, b))
    return out


def grey(pal):
    out = {}
    for k, v in pal.items():
        r, g, b, _ = hx(v)
        l = int((0.3 * r + 0.59 * g + 0.11 * b) * 0.75 + 20)
        out[k] = '#%02x%02x%02x' % (l, l, l + 6)
    return out


# ------------------------------------------------------------ the card-frame plate
def card_plate(w, h, pal, seed, frame=2):
    rng = np.random.default_rng(seed)
    img = np.zeros((h, w, 4), dtype=np.uint8)
    img[:, :] = BLACK
    for (y, x) in [(0, 0), (0, w - 1), (h - 1, 0), (h - 1, w - 1)]:
        img[y, x] = (0, 0, 0, 0)

    # grainy frame, darker toward the bottom
    fr, sh = hx(pal['frame']), hx(pal['shade'])
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            img[y, x] = sh if rng.random() < 0.28 + 0.35 * (y / h) else fr
    for (y, x) in [(1, 1), (1, w - 2), (h - 2, 1), (h - 2, w - 2)]:
        img[y, x] = BLACK

    # inner black line and a noisy fill, lighter at the top like the card backs
    t0 = 1 + frame
    img[t0:h - t0, t0:w - t0] = BLACK
    fy0, fy1, fx0, fx1 = t0 + 1, h - t0 - 1, t0 + 1, w - t0 - 1
    tones = [hx(pal['hi']), hx(pal['base']), hx(pal['mid']), hx(pal['dark'])]
    for y in range(fy0, fy1):
        for x in range(fx0, fx1):
            v = (y - fy0) / max(fy1 - fy0 - 1, 1) + rng.normal(0, 0.2)
            i = 0 if v < 0.1 else 1 if v < 0.55 else 2 if v < 0.97 else 3
            if rng.random() < 0.05:
                i = max(i - 1, 0)
            img[y, x] = tones[i]

    # drips off the inner line, chips in the outline
    if fx1 - fx0 <= 8:
        return img
    for _ in range(max(2, w // 18)):
        img[fy0:fy0 + rng.integers(1, 3), rng.integers(fx0 + 2, fx1 - 2)] = BLACK
    for _ in range(max(1, w // 30)):
        img[fy1 - 1, rng.integers(fx0 + 2, fx1 - 2)] = BLACK
    for _ in range(max(2, w // 16)):
        side = rng.integers(4)
        if side == 0: img[1, rng.integers(3, w - 3)] = BLACK
        if side == 1: img[h - 2, rng.integers(3, w - 3)] = BLACK
        if side == 2: img[rng.integers(3, h - 3), 1] = BLACK
        if side == 3: img[rng.integers(3, h - 3), w - 2] = BLACK
    return img


def mask_of(img):
    return img[:, :, 3] > 0


def with_shadow(plate, state):
    """The plate over its drop shadow: lifted a pixel on hover, pressed down
    onto the shadow when pressed. Every state is the same size, so swapping
    them doesn't move the button."""
    h, w = plate.shape[:2]
    out = np.zeros((h + DEPTH + 1, w, 4), dtype=np.uint8)
    m = mask_of(plate)
    if state != 'pressed':
        out[1 + DEPTH:1 + DEPTH + h][m] = (0, 0, 0, 110)
    oy = {'normal': 1, 'hover': 0, 'pressed': 1 + DEPTH, 'disabled': 1}[state]
    out[oy:oy + h][m] = plate[m]
    return out


def put_label(img, s, col, shadow, icon=None, gap=4):
    tm = text_mask(s)
    h, w = img.shape[:2]
    total = tm.shape[1] + ((icon.shape[1] + gap) if icon is not None else 0)
    x, y = (w - total) // 2, (h - 7) // 2
    if icon is not None:
        iy = y + (7 - icon.shape[0]) // 2
        stamp(img, icon, x, iy + 1, shadow)
        stamp(img, icon, x, iy, col)
        x += icon.shape[1] + gap
    stamp(img, tm, x, y + 1, shadow)
    stamp(img, tm, x, y, col)


def styled(pal, state):
    if state == 'disabled':
        return grey(pal)
    return shift(pal, {'normal': 0, 'hover': 1, 'pressed': -1}[state])


def label_colours(pal, state):
    return (DIM_TEXT, DIM_SHADOW) if state == 'disabled' else (CREAM, hx(pal['dark']))


# ------------------------------------------------------------ the buttons
def submit(state):
    pal = styled(CARD['green'], state)
    plate = card_plate(66, 22, pal, seed_of('submit'))
    put_label(plate, 'SUBMIT', *label_colours(pal, state))
    return with_shadow(plate, state)


def refresh(state, left):
    pal = styled(CARD['teal'], state)
    w, h = 76, 22
    plate = card_plate(w, h, pal, seed_of('refresh'))
    put_label(plate, 'REFRESH', *label_colours(pal, state))

    # the tab on top, holding one diamond per refresh left
    tw, th, rise = 21, 9, 6
    tab = card_plate(tw, th, pal, seed_of('tab'), frame=0)
    tab[1:th - 1, 1:tw - 1] = hx(pal['frame'])
    lit = CREAM if state != 'disabled' else DIM_TEXT
    for i in range(2):
        stamp(tab, PIP_FULL if i < left else PIP_EMPTY, 4 + i * 9, 2, lit if i < left else EMPTY_PIP)
    full = np.zeros((h + rise, w, 4), dtype=np.uint8)
    full[rise:] = plate
    tx = (w - tw) // 2
    tm = mask_of(tab)
    full[0:th, tx:tx + tw][tm] = tab[tm]
    return with_shadow(full, state)


def menu(state):
    pal = styled(CARD['slate'], state)
    plate = card_plate(66, 20, pal, seed_of('menu'))
    put_label(plate, 'MENU', *label_colours(pal, state), icon=ICON_BACK)
    return with_shadow(plate, state)


def save(img, name):
    big = np.kron(img, np.ones((SCALE, SCALE, 1), dtype=np.uint8))
    Image.fromarray(big, 'RGBA').save(os.path.join(OUT, name + '.png'))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for state in ('normal', 'hover', 'pressed'):
        save(submit(state), f'submit_{state}')
        save(menu(state), f'menu_{state}')
    for left in (1, 2):
        for state in ('normal', 'hover', 'pressed', 'disabled'):  # disabled while the new hand deals in
            save(refresh(state, left), f'refresh{left}_{state}')
    save(refresh('disabled', 0), 'refresh0_disabled')
    print('saved to', os.path.normpath(OUT))
