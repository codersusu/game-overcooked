#!/usr/bin/env python3
"""Resumable first production batch. Uses credentials only inside meshy_test.api.

Every paid POST is recorded before sending and is never blindly retried.
Only the 12 approved trio/outfit combinations are allowed in this batch.
"""
import argparse
import json
import time
from pathlib import Path
import meshy_test as client

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'art/production/round-01/characters'
ALLOWED = {s + '-' + o for s in ('capybara', 'cat', 'dog') for o in
           ('male-chef', 'female-chef', 'male-customer', 'female-customer')}
REQUEST = dict(ai_model='meshy-6', model_type='standard', should_texture=True,
               enable_pbr=False, should_remesh=True, topology='triangle',
               target_polycount=12000, pose_mode='a-pose', texture_resolution='2k',
               image_enhancement=False, remove_lighting=True,
               target_formats=['glb', 'fbx'], multi_view_thumbnails=True)

def select(name):
    if name not in ALLOWED:
        raise ValueError('Not part of the approved initial production trio: ' + name)
    client.CACHE = ROOT / '.local/meshy-production-round-01' / name
    client.OUTPUT = OUT / name
    client.OUTPUT.mkdir(parents=True, exist_ok=True)
    if not (client.OUTPUT / 'input.png').exists():
        raise ValueError('Missing single-character reference: ' + name)
    client.save(client.OUTPUT / 'image-to-3d-request.json', REQUEST)

def run(names):
    for name in names:
        select(name)
        _, task = client.record('model')
        if not task:
            print(name, 'submitting 30-credit textured model', flush=True)
            client.submit('model')
        elif not task.get('id'):
            raise RuntimeError('Uncertain earlier submission; reconcile before retry: ' + name)
    pending = set(names)
    while pending:
        for name in list(pending):
            select(name)
            _, task = client.record('model')
            if task['status'] not in ('SUCCEEDED', 'FAILED', 'CANCELED'):
                print(name, end=' ', flush=True)
                client.poll('model')
                _, task = client.record('model')
            if task['status'] in ('FAILED', 'CANCELED'):
                print(name, 'model did not succeed; no automatic paid retry', flush=True)
                pending.remove(name)
                continue
            if task['status'] != 'SUCCEEDED':
                continue
            marker = client.CACHE / 'model-downloaded.json'
            if not marker.exists():
                client.download('model')
                client.save(marker, {'complete': True})
            _, rig = client.record('rig')
            if not rig:
                print(name, 'submitting 5-credit auto-rig', flush=True)
                try:
                    client.submit('rig')
                except RuntimeError as exc:
                    # A synchronous 422 explicitly rejects the input before a task exists.
                    # Record that rejection; do not turn it into an ambiguous retry.
                    if 'Meshy HTTP 422:' not in str(exc):
                        raise
                    client.save(client.CACHE / 'rig.json', {'status':'FAILED', 'consumed_credits':0,
                                'task_error':{'message':client.safe(exc)}})
                    client.save(client.OUTPUT / 'rig-status.json', {'status':'FAILED', 'consumed_credits':0,
                                'task_error':{'message':client.safe(exc)}})
                    pending.remove(name)
                    print(name, 'auto-rig input rejected; static model retained', flush=True)
                    continue
                _, rig = client.record('rig')
            elif not rig.get('id') and rig.get('status') != 'FAILED':
                raise RuntimeError('Uncertain earlier rig submission: ' + name)
            if rig['status'] not in ('SUCCEEDED', 'FAILED', 'CANCELED'):
                print(name, end=' ', flush=True)
                client.poll('rig')
                _, rig = client.record('rig')
            if rig['status'] == 'SUCCEEDED':
                client.download('rig')
                pending.remove(name)
                print(name, 'model and auto-rig saved', flush=True)
            elif rig['status'] in ('FAILED', 'CANCELED'):
                print(name, 'rig failed; static model retained; no automatic paid retry', flush=True)
                pending.remove(name)
        if pending:
            time.sleep(20)
    audit = []
    for name in sorted(ALLOWED):
        base = ROOT / '.local/meshy-production-round-01' / name
        entry = {'character': name}
        for stage in ('model', 'rig'):
            p = base / (stage + '.json')
            if p.exists():
                entry[stage] = client.brief(stage, json.loads(p.read_text()))
        if len(entry) > 1:
            audit.append(entry)
    client.save(OUT.parent / 'meshy-audit.json', audit)
    print('Batch invocation complete.', flush=True)

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('characters', nargs='+')
    args = parser.parse_args()
    try:
        run(args.characters)
    except Exception as exc:
        print(client.safe(exc), flush=True)
        raise SystemExit(1)
