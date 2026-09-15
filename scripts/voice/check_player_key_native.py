"""Opt-in native transport check against a keyless helper; no hardware microphone."""
import argparse
import asyncio
import base64
import json
from pathlib import Path
from aiohttp import ClientSession, WSMsgType
from chef_brain import ROOT, load_key

async def run(port):
    context = json.loads((ROOT / 'art/production/gameplay-round-01/qa/voice/context-level-2.json').read_text())
    state = {'started': False, 'audio': 0, 'stopping': False, 'finalized': False}
    errors = []
    async with ClientSession() as http:
        async with http.ws_connect(f'http://127.0.0.1:{port}/api/live/native', headers={'X-Bara-Help': '1'}) as ws:
            await ws.send_json({'context': context, 'apiKey': load_key(ROOT / '.env')})
            async def silence():
                frame = base64.b64encode(bytes(1920)).decode()
                n = 0
                while not state['stopping']:
                    if state['started']:
                        await ws.send_json({'type': 'audio', 'audio': frame})
                        if n % 50 == 0:
                            await ws.send_json({'type': 'context', 'context': context})
                        n += 1
                    await asyncio.sleep(.04)
            pump = asyncio.create_task(silence())
            try:
                async for message in ws:
                    if message.type != WSMsgType.TEXT:
                        continue
                    event = json.loads(message.data)
                    kind = event.get('type')
                    if kind == 'session.started':
                        state['started'] = True
                    elif kind == 'session.output_audio.delta':
                        state['audio'] += len(base64.b64decode(event['delta']))
                        if not state['stopping']:
                            state['stopping'] = True
                            await pump
                            await ws.send_json({'type': 'close'})
                    elif kind == 'bara.closed':
                        state['finalized'] = event.get('finalized', False)
                    elif kind in ('bara.error', 'error') and not state['stopping']:
                        errors.append('Native connection error')
            finally:
                pump.cancel()
    report = {'passed': state['started'] and state['audio'] > 0 and state['finalized'] and not errors,
              'keylessHelper': True, 'playerKeySupplied': True, 'syntheticSilenceOnly': True,
              'sessionStarted': state['started'], 'replyPcmBytes': state['audio'],
              'finalized': state['finalized'], 'errors': errors}
    (ROOT / 'art/production/gameplay-round-01/qa/voice/native-player-key.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report))
    assert report['passed']

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--live', action='store_true')
    parser.add_argument('--port', type=int, default=54116)
    args = parser.parse_args()
    if not args.live:
        raise SystemExit('Pass --live for the paid synthetic check.')
    asyncio.run(asyncio.wait_for(run(args.port), 45))
