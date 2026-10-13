"""Softens the game's sound effects (Tools/Sounds/raw, Kenney's Casino Audio,
CC0) into Assets/Resources/Sounds.

The raw clips are close-miked and bone dry, so they sound like they're
happening inside your head. Each one is:
  - compressed, so the sharp first click doesn't stick out above the rest,
  - rounded off at the very top end, taking the edge off the crispness,
  - put in a small room: a short stereo reverb tail, a little behind the
    dry sound, so it sits on a table in front of you.

Baked into the files because Unity's reverb filters don't run in WebGL.

Run from anywhere with Python 3 + numpy + scipy + soundfile:
    python Tools/Sounds/soften_sounds.py
"""
import os

import numpy as np
import soundfile as sf
from scipy.signal import butter, fftconvolve, lfilter

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, "raw")
OUT = os.path.join(HERE, "..", "..", "Assets", "Resources", "Sounds")

# Compressor
THRESHOLD_DB = -24.0
RATIO = 4.0
ATTACK = 0.002   # s
RELEASE = 0.060  # s

LOWPASS_HZ = 7000.0  # top-end roll-off

# Room
ROOM_SECONDS = 0.225  # time for the tail to fall 60 dB
PRE_DELAY = 0.006     # s, the first reflections arriving after the dry sound
WET = 0.11            # tail level against the dry sound
ROOM_LOWPASS_HZ = 4000.0  # walls soak up the highs

OUTPUT_PEAK = 0.8     # of the raw clip's peak: a touch quieter, each clip keeping its level against the others


def compress(x, rate):
    level = np.abs(x)
    env = np.zeros_like(level)
    a_att = np.exp(-1.0 / (ATTACK * rate))
    a_rel = np.exp(-1.0 / (RELEASE * rate))
    e = 0.0
    for i, v in enumerate(level):
        a = a_att if v > e else a_rel
        e = a * e + (1 - a) * v
        env[i] = e
    env_db = 20 * np.log10(env + 1e-9)
    over = np.maximum(env_db - THRESHOLD_DB, 0.0)
    return x * 10 ** (-over * (1 - 1 / RATIO) / 20)


def lowpass(x, rate, hz):
    b, a = butter(2, hz / (rate / 2))
    return lfilter(b, a, x, axis=0)


# Decaying noise, different in each ear so the room has width.
def room_impulse(rate, rng):
    n = int(ROOM_SECONDS * rate)
    t = np.arange(n) / rate
    decay = 10 ** (-3 * t / ROOM_SECONDS)  # -60 dB at ROOM_SECONDS
    ir = rng.standard_normal((n, 2)) * decay[:, None]
    ir = lowpass(ir, rate, ROOM_LOWPASS_HZ)
    ir = np.vstack([np.zeros((int(PRE_DELAY * rate), 2)), ir])
    return ir / np.sqrt(np.sum(ir ** 2, axis=0))


def soften(path, rng):
    x, rate = sf.read(path)
    if x.ndim > 1:
        x = x.mean(axis=1)
    peak = np.abs(x).max()
    x = compress(x, rate)
    x = lowpass(x, rate, LOWPASS_HZ)

    ir = room_impulse(rate, rng)
    tail = np.stack([fftconvolve(x, ir[:, c]) for c in range(2)], axis=1)
    dry = np.zeros_like(tail)
    dry[: len(x)] = x[:, None]
    y = dry + WET * tail

    # Trim the inaudible end of the tail.
    loud = np.nonzero(np.abs(y).max(axis=1) > np.abs(y).max() * 10 ** (-60 / 20))[0]
    y = y[: loud[-1] + 1]
    return y / np.abs(y).max() * peak * OUTPUT_PEAK, rate


def main():
    rng = np.random.default_rng(7)  # same room every run
    for name in sorted(os.listdir(RAW)):
        if not name.endswith(".ogg"):
            continue
        y, rate = soften(os.path.join(RAW, name), rng)
        sf.write(os.path.join(OUT, name), y, rate, format="OGG", subtype="VORBIS")
        print(f"{name}: {len(y) / rate:.2f} s")


if __name__ == "__main__":
    main()
