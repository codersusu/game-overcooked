"""Opt-in paid GPT-Live native-transport test. Uses generated speech, never a hardware microphone."""
import argparse,asyncio,base64,copy,json,time,wave,os
from pathlib import Path
from aiohttp import ClientSession,WSMsgType
from chef_brain import ROOT,as_json,load_key,openai_call

QA=ROOT/'art/production/gameplay-round-01/qa/voice'
PRIVATE=ROOT/'.local/voice'
FIXTURES=ROOT/'scripts/voice/fixtures'
def speech(name,text):
 path=FIXTURES/(name+'.wav')
 if not path.exists():path=PRIVATE/(name+'.wav')
 if not path.exists():path.write_bytes(openai_call(load_key(ROOT/'.env'),'audio/speech',as_json({'model':'gpt-4o-mini-tts','voice':'coral','input':text,'response_format':'wav'}),binary=True))
 with wave.open(str(path),'rb') as f:
  assert f.getframerate()==24000 and f.getnchannels()==1 and f.getsampwidth()==2
  return f.readframes(f.getnframes())
async def main():
 c=json.loads((QA/'context-level-2.json').read_text());c.update(practice=False,score=240,served=3,missed=1,streak=2,orders=[{'id':1,'recipe':'Woodland soup','secondsLeft':90}])
 pot=next(s for s in c['stations'] if s['kind']=='pot');pot.update(heat='Cooking',potDocked=True,potIngredients=['chopped carrot','chopped mushroom'])
 clips=[(5,speech('performance','How am I doing so far?')),(42,speech('recovery','The fire is out. What should I do next?'))]
 # Reproducible browser fake microphone input: eight quiet seconds, then one question.
 question=speech('question','What should I do first to make a salad?')
 with wave.open(str(PRIVATE/'browser-question.wav'),'wb') as f:
  f.setparams((1,2,24000,0,'NONE','not compressed'));f.writeframes(b'\0'*(24000*2*8)+question+b'\0'*(24000*2*80))
 checks=[];transcripts=[];events=[];audio=[];started=asyncio.Event();closed=asyncio.Event();phase='progress';began=time.monotonic()
 def check(ok,label):
  if not ok:raise AssertionError(label)
  checks.append(label);print('PASS',label,flush=True)
 async with ClientSession() as http:
  async with http.ws_connect('http://127.0.0.1:'+os.environ.get('BARA_VOICE_PORT','54115')+'/api/live/native',headers={'X-Bara-Help':'1'}) as ws:
   await ws.send_json({'context':c,'apiKey':load_key(ROOT/'.env')})
   async def reader():
    async for message in ws:
     if message.type!=WSMsgType.TEXT:continue
     e=json.loads(message.data);k=e.get('type','')
     if k=='session.started':started.set()
     elif k=='session.output_audio.delta':audio.append(base64.b64decode(e['delta']))
     elif k in ('session.input_transcript.delta','session.output_transcript.delta'):transcripts.append({'seconds':round(time.monotonic()-began,2),'phase':phase,'type':k,'text':e['delta']})
     elif k=='session.closed':events.append({'type':k,'usage':e.get('usage')});closed.set()
     elif k=='bara.closed':events.append({'type':k,'finalized':e.get('finalized')})
     else:events.append(e)
   reading=asyncio.create_task(reader())
   try:
    await asyncio.wait_for(started.wait(),25);check(True,'Native PCM WebSocket reaches GPT-Live session.started');began=time.monotonic()
    for n in range(2350):
     second=n*.04
     if n==500:phase='warning';pot.update(heat='Warning',warningSecondsLeft=8)
     if n==700:phase='burning';pot.update(heat='Burning',warningSecondsLeft=0)
     if n==950:
      phase='recovery';pot['heat']='Extinguished';c['holding']={'kind':'Extinguisher','label':'Fire extinguisher'}
      for station in c['stations']:station['possibleActions']=['E: put down Fire extinguisher'] if station['kind'] in ('counter','extinguisher') else []
     if n==1550:
      phase='idle';c['idleSeconds']=20;c['holding']={};pot.update(heat='Empty',potIngredients=[])
      for station in c['stations']:
       station['possibleActions']=['E: pick up raw '+station['kind']] if station['kind'] in ('carrot','mushroom') else []
     frame=b'\0'*1920
     for start,pcm in clips:
      offset=round((second-start)*24000)*2
      if 0<=offset<len(pcm):frame=pcm[offset:offset+1920].ljust(1920,b'\0');break
     await ws.send_json({'type':'audio','audio':base64.b64encode(frame).decode()})
     if n%50==0:await ws.send_json({'type':'context','context':c})
     await asyncio.sleep(max(0,began+(n+1)*.04-time.monotonic()))
   finally:
    await ws.send_json({'type':'close'})
    try:await asyncio.wait_for(closed.wait(),12)
    except asyncio.TimeoutError:pass
    await reading
 joined={p:''.join(t['text'] for t in transcripts if t['phase']==p and t['type']=='session.output_transcript.delta') for p in ('progress','warning','burning','recovery','idle')}
 print(json.dumps(joined,ensure_ascii=False,indent=2),flush=True)
 report={'passed':False,'model':'gpt-live-1','syntheticAudioOnly':True,'stateSource':'Fixtures exported from actual kitchens; hazards/performance modified for controlled tests','checks':checks,'spokenReplies':joined,'events':events,'audioBytes':sum(map(len,audio))}
 (PRIVATE/'native-live-reply.pcm').write_bytes(b''.join(audio))
 try:validate_report(report)
 finally:(QA/'native-live.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')

def validate_report(report):
 # Accept natural wording, rather than requiring a specific noun in speech fragments.
 joined=report['spokenReplies'];events=report['events'];report['passed']=False
 checks=report['checks']=['Native PCM WebSocket reaches GPT-Live session.started']
 def check(ok,label):
  if not ok:raise AssertionError(label)
  checks.append(label);print('PASS',label,flush=True)
 check(report['audioBytes']>24000,'Native receives continuous PCM reply audio')
 check(any(x in joined['progress'].lower() for x in ('three','3','240','two hundred')),'Performance reply uses fixture progress')
 check(any(x in joined['warning'].lower() for x in ('pot','soup')) and any(x in joined['warning'].lower() for x in ('ready','warning','burn','attention')),'Warning phase produces a spoken soup/pot reminder')
 check(any(x in joined['burning'].lower() for x in ('burning','fire','extinguisher')),'Actual fire receives an urgent spoken reminder')
 check(('bin' in joined['recovery'].lower() or 'burnt' in joined['recovery'].lower() or ('put down' in joined['recovery'].lower() and 'pressing e' in joined['recovery'].lower())) and any(e.get('targetId')=='extinguisher' for e in events),'Post-fire advice starts recovery by freeing occupied paws at the stand')
 check(any(x in joined['idle'].lower() for x in ('carrot','mushroom','crate')),'Idle player gets a concrete next-step hint')
 check(any(e.get('type')=='session.closed' and e.get('usage',{}).get('seconds',0)>0 for e in events) and any(e.get('finalized') for e in events),'Native closes with final session usage')
 check(not any(e.get('type')=='bara.error' for e in events),'No native gateway errors')
 report['passed']=True

if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--live',action='store_true');parser.add_argument('--check-saved',action='store_true',help='Recheck the saved synthetic transcript without API calls');args=parser.parse_args()
 if args.check_saved:
  path=QA/'native-live.json';report=json.loads(path.read_text())
  try:validate_report(report)
  finally:path.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
 elif args.live:PRIVATE.mkdir(parents=True,exist_ok=True);asyncio.run(main())
 else:raise SystemExit('Pass --live for synthetic paid API tests, or --check-saved for existing evidence.')
