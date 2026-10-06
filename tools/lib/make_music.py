#!/usr/bin/env python3
"""Generates Urban Runner's original background loop (self-made, no third-party samples).
95 BPM, 8 bars (Am7 - Fmaj7 - Cmaj7 - G6, two bars each): soft pad, plucked arpeggio, bass, kick, hats, clap.
Seamless loop: every voice is rendered with wrap-around, so the tail of bar 8 bleeds into bar 1.
Usage: make_music.py <out.wav>"""
import math, struct, sys, wave

SR = 22050
BPM = 95
BEAT = 60.0 / BPM
BARS = 8
N = int(SR * BEAT * 4 * BARS)
buf = [0.0] * N

def note(m):  # MIDI -> Hz
    return 440.0 * 2 ** ((m - 69) / 12.0)

def add(start_s, dur_s, fn, gain):
    s0 = int(start_s * SR)
    for i in range(int(dur_s * SR)):
        buf[(s0 + i) % N] += fn(i / SR) * gain

# deterministic noise
_seed = [12345]
def rnd():
    _seed[0] = (1103515245 * _seed[0] + 12345) & 0x7FFFFFFF
    return _seed[0] / 0x3FFFFFFF - 1.0

chords = [[57, 60, 64, 67], [53, 57, 60, 64], [48, 55, 59, 64], [55, 59, 62, 64]]  # Am7 Fmaj7 Cmaj7 G6
bass = [45, 41, 36, 43]

for bar in range(BARS):
    ch = chords[(bar // 2) % 4]
    t0 = bar * 4 * BEAT
    # Pad: soft additive tone with slow attack/release, one per chord tone.
    if bar % 2 == 0:
        for m in ch:
            f = note(m + 12)
            def pad(t, f=f):
                env = min(1.0, t / 0.6) * min(1.0, (8 * BEAT - t) / 0.8)
                return env * (math.sin(2 * math.pi * f * t) + 0.35 * math.sin(2 * math.pi * f * 2.003 * t) + 0.15 * math.sin(2 * math.pi * f * 2.997 * t))
            add(t0, 8 * BEAT, pad, 0.045)
    # Arpeggio: 8th notes, plucked.
    pattern = [0, 2, 1, 3, 2, 1, 3, 2]
    for k in range(8):
        f = note(ch[pattern[k]] + 24)
        def pluck(t, f=f):
            return math.exp(-t * 7.0) * (math.sin(2 * math.pi * f * t) + 0.25 * math.sin(4 * math.pi * f * t))
        add(t0 + k * BEAT / 2, BEAT * 1.2, pluck, 0.07 if k % 2 == 0 else 0.05)
    # Bass: root on 1 and the "and" of 2, octave on 3.
    for beat, mult in ((0, 1), (1.5, 1), (2, 2), (3.5, 1)):
        f = note(bass[(bar // 2) % 4]) * mult
        def b(t, f=f):
            return math.exp(-t * 2.5) * (math.sin(2 * math.pi * f * t) + 0.3 * math.sin(4 * math.pi * f * t))
        add(t0 + beat * BEAT, BEAT * 0.9, b, 0.16)
    # Drums.
    for beat in range(4):
        def kick(t):
            f = 55 + 90 * math.exp(-t * 30)
            return math.exp(-t * 9) * math.sin(2 * math.pi * f * t)
        if beat in (0, 2):
            add(t0 + beat * BEAT, 0.35, kick, 0.5)
        if beat in (1, 3):
            add(t0 + beat * BEAT, 0.22, lambda t: math.exp(-t * 22) * rnd(), 0.18)  # clap
        for half in (0, 0.5):
            add(t0 + (beat + half) * BEAT, 0.05, lambda t: math.exp(-t * 90) * rnd(), 0.05)  # hat

peak = max(abs(x) for x in buf) or 1.0
scale = 0.85 / peak
with wave.open(sys.argv[1], "wb") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, x * scale)) * 32767)) for x in buf))
print(f"wrote {sys.argv[1]}: {N / SR:.1f}s loop")
