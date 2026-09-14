"""Generate a bounded, reusable Bara Kitchen sound pack; never export credentials."""
from pathlib import Path
import urllib.request, urllib.error, json, sys
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'art/audio/round-01';OUT.mkdir(parents=True,exist_ok=True)
settings={line.split('=',1)[0].strip():line.split('=',1)[1].strip().strip('\"\'') for line in (ROOT/'.env').read_text().splitlines() if '=' in line and not line.lstrip().startswith('#')}
key=settings['ELEVEN_LABS_API_KEY']
jobs=[
 ('chop','sfx',.7,False,'One crisp small kitchen knife chopping a fresh cucumber on a wooden board, one short soft woody tok and tiny vegetable crunch. Charming cozy cooking game. Dry isolated close sound, immediate attack, silence after, no music, no voice.'),
 ('wash','sfx',3,True,'Gentle continuous washing of one ceramic dish with a wet sponge, soft running tap water, tiny bubbles and delicate ceramic rub. Cozy cartoon kitchen, close dry sound, seamless even loop, no music, no voice.'),
 ('guest-happy','sfx',1.4,False,'A tiny friendly rounded cartoon animal makes a soft delighted nonverbal mm-mm! ooh! on receiving delicious food. Cute warm squeaky creature vocalization, no intelligible words, no language, no music, no background. Short isolated gentle sound.'),
 ('guest-eat','sfx',1.3,False,'A cute small cartoon animal quietly enjoying one bite, tiny playful nom nom mouth sounds and a soft pleased humming chirp. No intelligible words, no speech, no music. Very short isolated clean adorable creature foley, not gross.'),
 ('plate','sfx',.5,False,'One gentle ceramic plate placed onto a wooden kitchen worktop, soft warm clink and tiny wooden tap. Short isolated cozy game foley, immediate attack, no music, no voice.'),
 ('spray','sfx',2.5,True,'Soft steady fire extinguisher foam spray hiss, gentle airy shhhh, seamless loop for a friendly cartoon kitchen game. No siren, no voice, no music, not harsh.'),
 ('fire','sfx',3,True,'Small kitchen fire crackling, light bubbling flame and soft crackles, seamless quiet cartoon game ambience loop. No sirens, no explosion, no speech, no music.'),
 ('music-service','music',48,False,'Original cozy playful instrumental background music for a cute capybara cafe cooking game. 100 BPM, C major, warm pizzicato strings, gentle marimba and toy piano melody, light acoustic ukulele, rounded upright bass, soft brushed percussion. Cheerful delicate bouncy and relaxing, polished game soundtrack, steady groove, no vocals, no words, no dramatic intro or final cadence, designed to repeat with a short crossfade.'),
 ('music-hurry','music',18,False,'Original instrumental cute animal kitchen game final thirty seconds hurry music. 132 BPM, C major playful marimba and pizzicato ostinato, light woodblock ticking, rounded bass and little flute accents. Urgent but friendly and comic, clear rhythmic pulse, not scary, no vocals, no alarm sirens. Steady loopable instrumental cue, no long intro or ending.'),
 ('music-fire','music',18,False,'Original instrumental short cartoon cafe kitchen fire emergency music. 140 BPM, playful minor pizzicato strings, bouncing bassoon, marimba runs and soft snare taps. Comical little kitchen mishap, busy and urgent but cute, not frightening. Consistent loopable game cue, no vocals, no speech, no sirens, no long intro or ending.')]
manifest=json.loads((OUT/'generation.json').read_text()) if (OUT/'generation.json').exists() else {'provider':'ElevenLabs','assets':[]}
for name,kind,seconds,loop,prompt in jobs:
 target=OUT/(name+'.mp3')
 if target.exists() and target.stat().st_size>1000:print('EXISTS',name,flush=True);continue
 body={'prompt':prompt,'music_length_ms':int(seconds*1000),'force_instrumental':True,'model_id':'music_v1'} if kind=='music' else {'text':prompt,'duration_seconds':seconds,'loop':loop,'model_id':'eleven_text_to_sound_v2','prompt_influence':.45}
 endpoint='music' if kind=='music' else 'sound-generation'
 request=urllib.request.Request('https://api.elevenlabs.io/v1/'+endpoint+'?output_format=mp3_44100_128',data=json.dumps(body).encode(),headers={'xi-api-key':key,'Content-Type':'application/json'},method='POST')
 try:
  with urllib.request.urlopen(request,timeout=240) as response:
   data=response.read();cost=response.headers.get('character-cost');target.write_bytes(data)
  manifest['assets']=[a for a in manifest['assets'] if a['id']!=name]+[{'id':name,'kind':kind,'seconds':seconds,'loopRequested':loop,'prompt':prompt,'file':target.name,'bytes':len(data),'reportedCreditCost':cost}]
  (OUT/'generation.json').write_text(json.dumps(manifest,indent=2)+'\n');print('GENERATED',name,len(data),'bytes',flush=True)
 except urllib.error.HTTPError as e:
  print('API_STATUS',name,e.code,flush=True)
  # Avoid repeated charges or authentication retries. Finish unaffected local work.
  if e.code in (401,402,403):sys.exit(2)
  raise RuntimeError('Audio generation failed with HTTP '+str(e.code)) from None
