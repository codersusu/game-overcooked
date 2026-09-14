#!/usr/bin/env python3
"""Encode browser-rendered animation frames as small, shareable GIFs."""
import json
from pathlib import Path
from PIL import Image, ImageOps, ImageDraw

ROOT = Path(__file__).resolve().parents[1] / 'art/experiments/meshy-capybara-01'
results = json.loads((ROOT / 'viewer-check.json').read_text())['results']
for result in results:
    name = result['name']
    if name not in ('walk', 'run', 'jump'):
        continue
    paths = sorted((ROOT / 'motion-frames' / name).glob('*.png'))
    frames = [Image.open(p).convert('RGB').resize((560, 392), Image.Resampling.LANCZOS) for p in paths]
    samples = frames[::max(1, len(frames)//12)]
    palette_source = Image.new('RGB', (160*len(samples), 112))
    for i, frame in enumerate(samples):
        palette_source.paste(frame.resize((160, 112)), (i*160, 0))
    palette = palette_source.quantize(colors=128)
    quantized = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    # GIF timing uses centiseconds. Distribute rounding to preserve clip length.
    step = result['duration'] * 100 / len(frames)
    durations = [max(10, (round((i+1)*step)-round(i*step))*10) for i in range(len(frames))]
    output = ROOT / 'screenshots' / (name + '.gif')
    quantized[0].save(output, save_all=True, append_images=quantized[1:], duration=durations, loop=0, optimize=False)
    print(output.name, len(frames), 'frames;', output.stat().st_size, 'bytes')

# Selected actual rendered poses for visual review; no generated imagery.
sheet = Image.new('RGB', (1200, 616), '#f5efe5')
draw = ImageDraw.Draw(sheet)
for col, (name, label) in enumerate((('walk','Walking'), ('run','Running'), ('jump','Happy jump'))):
    draw.text((col*400+16,10), label, fill='#30291f')
    for row, index in enumerate((1, 3)):
        frame = Image.open(ROOT/'screenshots'/f'{name}-frame-{index}.png').convert('RGB')
        sheet.paste(ImageOps.contain(frame,(400,280)), (col*400, 28+row*292))
sheet.save(ROOT/'screenshots'/'motion-contact-sheet.png')
