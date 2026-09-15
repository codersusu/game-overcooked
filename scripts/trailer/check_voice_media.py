"""Validate decoded bookends and audio levels of the finished voice trailer."""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'art/trailers/round-02'
FF = os.environ.get('FFMPEG_BIN') or shutil.which('ffmpeg') or str(
    ROOT / '.local/audio-tools/imageio_ffmpeg/binaries/ffmpeg-macos-aarch64-v7.1')
movie = OUT / 'Bara-Kitchen-Voice-Trailer.mp4'
edit = json.loads((OUT / 'edit.json').read_text())
closing_window = edit['timeline'][-1]['start'] - 0.6

# Decode the first/last three seconds. Compare all 90 frames, without assuming
# that AAC encoder padding leaves the final container's first video PTS at zero.
result = subprocess.run([
    FF, '-hide_banner', '-i', str(movie), '-vf',
    f'select=lt(n\\,90)+gte(t\\,{closing_window})', '-an', '-fps_mode', 'passthrough',
    '-f', 'framemd5', '-'
], capture_output=True, text=True, check=True)
frames = [line.split(',')[-1].strip() for line in result.stdout.splitlines()
          if line and not line.startswith('#')]
assert len(frames) >= 180 and frames[:90] == frames[-90:], 'Title bookends differ'

audio = subprocess.run([
    FF, '-hide_banner', '-i', str(movie), '-vn', '-af', 'volumedetect',
    '-f', 'null', '-'
], capture_output=True, text=True, check=True)
peak = float(re.search(r'max_volume: ([\-\d.]+) dB', audio.stderr).group(1))
mean = float(re.search(r'mean_volume: ([\-\d.]+) dB', audio.stderr).group(1))
assert peak < -0.1, 'Audio has insufficient clipping headroom'
report = {
    'passed': True, 'matchingOpeningClosingFrames': 90,
    'bookendSeconds': 3, 'audioPeakDbfs': peak, 'audioMeanDbfs': mean,
    'note': 'All opening/closing decoded frames match; AAC padding is tolerated. '
            'Whole-file duration, dimensions and playback are checked separately.'
}
(OUT / 'qa/encoding-validation.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report))
