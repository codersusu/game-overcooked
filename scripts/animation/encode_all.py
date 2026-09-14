#!/usr/bin/env python3
import json,subprocess,sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
out=root/'art/production/animations-round-01'
for item in json.loads((out/'clips.json').read_text())['clips']:
 if len(sys.argv)>1 and item['id'] not in sys.argv[1:]:continue
 subprocess.run([str(root/'.local/encode-animation'),str(root/'.local/animation-frames'/item['id']),str(out/item['video'])],check=True)
