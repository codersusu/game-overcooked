"""Generate clearly identified synthetic player questions for the voice feature trailer."""
import sys,wave,json
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'scripts/voice'))
from chef_brain import load_key,openai_call,as_json
OUT=ROOT/'art/trailers/round-02/source';OUT.mkdir(parents=True,exist_ok=True)
questions={'chop':'Bara, how do I chop this tomato?','progress':'How am I doing so far?','casual':'This little cafe feels cozy, huh?','cutest':'What is the cutest animal in this kitchen?'}
def make(entry):
 name,text=entry;p=OUT/(name+'-question.wav')
 if not p.exists():
  raw=openai_call(load_key(ROOT/'.env'),'audio/speech',as_json({'model':'gpt-4o-mini-tts','voice':'coral','input':text,'instructions':'A relaxed adult player speaking naturally to a game companion. Clear, conversational, neutral pitch. One short question, no acting or cute character voice.','response_format':'pcm'}),binary=True)
  with wave.open(str(p),'wb') as f:f.setparams((1,2,24000,0,'NONE','not compressed'));f.writeframes(raw)
 return name
with ThreadPoolExecutor(max_workers=4) as pool:print('Prepared questions:',list(pool.map(make,questions.items())))
(OUT/'questions.json').write_text(json.dumps({'source':'Synthetic player questions for recording; assistant replies are captured from the live game','model':'gpt-4o-mini-tts','voice':'coral','questions':questions},indent=2)+'\n')
