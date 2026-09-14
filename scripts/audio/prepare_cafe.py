from pathlib import Path
import sys,subprocess,json,wave
import numpy as np
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'.local/audio-tools'))
import imageio_ffmpeg
ffmpeg=imageio_ffmpeg.get_ffmpeg_exe();src=ROOT/'art/audio/round-01';out=ROOT/'Unity/BaraKitchen/Assets/BaraKitchen/Gameplay/Audio';out.mkdir(parents=True,exist_ok=True)
report=[];previews=[]
for p in sorted(src.glob('*.mp3')):
 music=p.stem.startswith('music-');channels=2 if music else 1
 raw=subprocess.check_output([ffmpeg,'-v','error','-i',str(p),'-f','f32le','-ar','44100','-ac',str(channels),'-'])
 a=np.frombuffer(raw,dtype='<f4').copy().reshape(-1,channels);a-=np.mean(a,axis=0)
 peak=np.max(np.abs(a));rms=np.sqrt(np.mean(a*a));gain=min(.88/max(peak,1e-6),(.12 if music else .14)/max(rms,1e-6));a*=gain
 # Soft edges avoid clicks; loop beds remain steady through the loop boundary.
 n=220;a[:n]*=np.linspace(0,1,n)[:,None];a[-n:]*=np.linspace(1,0,n)[:,None]
 target=out/(p.stem+'.wav')
 with wave.open(str(target),'wb') as w:w.setnchannels(channels);w.setsampwidth(2);w.setframerate(44100);w.writeframes((np.clip(a,-1,1)*32767).astype('<i2').tobytes())
 report.append({'id':p.stem,'seconds':round(len(a)/44100,3),'peak':round(float(np.max(np.abs(a))),3),'rms':round(float(np.sqrt(np.mean(a*a))),3),'channels':channels})
 if p.stem in ['music-service','music-fire','guest-happy','guest-eat','chop','wash']:
  mono=a.mean(axis=1)[:44100*(8 if music else 2)];previews.append(np.concatenate([mono,np.zeros(22050)]))
# Local original one-shots for common navigation, footsteps and clear success/failure cues.
rng=np.random.default_rng(314)
def tone(name,notes,unit=.11):
 size=int(44100*(unit*len(notes)+.2));v=np.zeros(size)
 for i,f in enumerate(notes):
  t=np.arange(int(44100*.20))/44100;s=(np.sin(2*np.pi*f*t)+.18*np.sin(2*np.pi*f*2*t))*np.exp(-t*19)*np.minimum(t/.005,1)*.22
  start=int(i*unit*44100);v[start:start+len(s)]+=s
 save(name,v)
def save(name,a):
 a=np.clip(a,-.8,.8)
 with wave.open(str(out/(name+'.wav')),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(44100);w.writeframes((a*32767).astype('<i2').tobytes())
 report.append({'id':name,'seconds':len(a)/44100,'source':'original local synthesis','peak':float(np.max(np.abs(a)))})
tone('success',[659.25,830.61,987.77,1318.5]);tone('ready',[783.99,1046.5]);tone('new-order',[659.25,880]);tone('warning',[659.25,523.25,659.25],.17);tone('expired',[392,329.63,261.63],.15);tone('ui',[880],.04)
t=np.arange(4410)/44100;save('step',(rng.normal(0,1,len(t))*.25+np.sin(2*np.pi*170*t)*.25)*np.exp(-t*65)*np.minimum(t/.003,1));t=np.arange(9702)/44100;noise=rng.normal(0,1,len(t));noise=np.convolve(noise,np.ones(9)/9,'same');save('whoosh',noise*np.sin(np.pi*t/.22)**2*.42)
with wave.open(str(src/'preview.wav'),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(44100);w.writeframes((np.concatenate(previews)*32767).astype('<i2').tobytes())
(src/'audio-validation.json').write_text(json.dumps(report,indent=2)+'\n');print('Prepared',len(report),'clips. FFmpeg:',ffmpeg)
