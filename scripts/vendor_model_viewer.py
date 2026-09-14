#!/usr/bin/env python3
from pathlib import Path
import urllib.request
out=Path(__file__).resolve().parents[1]/'art/production/round-01/vendor'
out.mkdir(parents=True,exist_ok=True)
for name,url in [
 ('model-viewer.min.js','https://unpkg.com/@google/model-viewer@4.1.0/dist/model-viewer.min.js'),
 ('model-viewer-LICENSE.txt','https://unpkg.com/@google/model-viewer@4.1.0/LICENSE')]:
 path=out/name
 if not path.exists():
  with urllib.request.urlopen(url,timeout=60) as response:path.write_bytes(response.read())
 print(name,path.stat().st_size)
