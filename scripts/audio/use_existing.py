"""Prepare CC0 field recordings; run after prepare_cafe.py (does not call an API)."""
from pathlib import Path
import subprocess,sys,wave,json
import numpy as np
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'.local/audio-tools'))
import imageio_ffmpeg
ff=imageio_ffmpeg.get_ffmpeg_exe();src=ROOT/'art/audio/sources';out=ROOT/'Unity/BaraKitchen/Assets/BaraKitchen/Gameplay/Audio';rate=44100
report=[]
def read(p):return np.frombuffer(subprocess.check_output([ff,'-v','error','-i',str(p),'-ac','1','-ar',str(rate),'-f','f32le','-']),dtype='<f4').copy()
def write(name,a,source,loop=False):
 a-=a.mean()
 if loop: a=.28*np.tanh(a/max(np.sqrt(np.mean(a*a)),1e-7)*.5)
 a*=min(.82/max(abs(a).max(),1e-7),.12/max(np.sqrt(np.mean(a*a)),1e-7))
 if loop:
  n=int(.12*rate);a=np.concatenate([a[n:-n],a[-n:]*(1-np.linspace(0,1,n))+a[:n]*np.linspace(0,1,n)])
 else:
  n=min(220,len(a)//4);a[:n]*=np.linspace(0,1,n);a[-n:]*=np.linspace(1,0,n)
 with wave.open(str(out/(name+'.wav')),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes((a*32767).astype('<i2').tobytes())
 report.append(dict(id=name,source=source,license='CC0',seconds=len(a)/rate,peak=float(abs(a).max()),rms=float(np.sqrt(np.mean(a*a)))))
# Isolate one strongest board strike instead of replaying a whole recording each stroke.
a=read(src/'chop-2070.mp3');energy=np.convolve(a*a,np.ones(220)/220,'same');peak=int(np.argmax(energy));start=max(0,peak-int(.07*rate));write('chop',a[start:start+int(.48*rate)],'Joseph SARDIN / BigSoundBank #2070')
a=read(src/'water-0651.mp3');write('wash',a[5*rate:9*rate],'Joseph SARDIN / BigSoundBank #0651',True)
k=src/'kenney-impact/Audio'
write('plate',read(k/'impactPlate_light_000.ogg'),'Kenney Impact Sounds / impactPlate_light_000')
write('step',read(k/'footstep_wood_000.ogg'),'Kenney Impact Sounds / footstep_wood_000')
write('pickup',read(k/'impactSoft_heavy_000.ogg'),'Kenney Impact Sounds / impactSoft_heavy_000')
if (src/'fire-0988.mp3').exists():
 a=read(src/'fire-0988.mp3');write('fire',a[3*rate:9*rate],'Joseph SARDIN / BigSoundBank #0988',True)
(src/'selected-assets.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
