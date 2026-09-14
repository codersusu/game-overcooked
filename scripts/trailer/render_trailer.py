"""Render the 48-second review trailer from captured Unity gameplay; no generation APIs."""
import json, os, shutil, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
os.chdir(ROOT)
OUT=ROOT/'art/trailers/round-01'; WORK=ROOT/'.local/trailer-edit'; WORK.mkdir(parents=True,exist_ok=True)
FF=os.environ.get('FFMPEG_BIN') or shutil.which('ffmpeg') or str(ROOT/'.local/audio-tools/imageio_ffmpeg/binaries/ffmpeg-macos-aarch64-v7.1')
FONT=str(ROOT/'Unity/BaraKitchen/Assets/BaraKitchen/Gameplay/UI/Fonts/LilitaOne.ttf')
SOURCE=OUT/'source'; SOURCE.mkdir(exist_ok=True)
# file, source start, edit seconds, crop rectangle (x,y,w,h), short feature caption
shots=[
 ('level-1-welcome',.2,3,None,'A little cafe of your own'),
 ('level-1-chopping',0,3,(500,155,960,540),'Chop'),
 ('level-1-assemble-salad',0,1.7,(475,180,960,540),'Combine'),
 ('level-1-delivery',0,2.8,(500,195,1120,630),'Serve'),
 ('level-1-customers',0,3.8,(470,510,960,540),'Make their day'),
 ('level-1-washing',0,3,(300,330,960,540),'Keep the kitchen flowing'),
 ('level-2-cooking-diagonal',0,4.3,(480,300,864,486),'From fresh ingredients to warm soup'),
 ('level-2-dashing',0,1.3,(440,165,1040,585),'Dash'),
 ('level-3-throwing',0,2,(435,230,1040,585),'Find a shortcut'),
 ('level-4-throwing',0,2,(360,170,1040,585),None),
 ('level-2-fire-and-rescue-diagonal',0,9,(480,300,864,486),'A little kitchen chaos'),
 ('level-4-delivery',0,3,None,'Four kitchens. Your rhythm.'),
 ('level-4-customers',0,3,(360,455,1200,675),'Little paws. Big appetites.'),
]
def run(args):
 subprocess.run([FF,'-hide_banner','-loglevel','error','-y',*args],check=True)
def encode(args,filters,seconds,path):
 run([*args,'-an','-vf',filters+',setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709','-frames:v',str(round(seconds*30)),'-r','30','-c:v','libx264','-preset','fast','-b:v','8M','-minrate','8M','-maxrate','8M','-bufsize','16M','-x264-params','nal-hrd=cbr','-color_primaries','bt709','-color_trc','bt709','-colorspace','bt709','-color_range','tv','-pix_fmt','yuv420p','-movflags','+faststart',str(path)])
# The very same encoded three seconds bookend the video.
title=WORK/'title.mp4'
encode(['-loop','1','-framerate','30','-i',str(OUT/'title-card.png')],"scale=3840:2160,zoompan=z='1+on*0.00038':x='iw/2-iw/zoom/2':y='ih/2-ih/zoom/2':d=1:s=1920x1080:fps=30,setsar=1",3,title)
segments=[title]; timeline=[{'start':0,'duration':3,'file':'title-card.png','kind':'opening'}];cursor=3
for i,(name,start,dur,crop,caption) in enumerate(shots):
 source=SOURCE/(name+'.webm'); raw=ROOT/'.local/trailer-capture'/(name+'.webm')
 if raw.exists():shutil.copy2(raw,source)
 if not source.exists():raise FileNotFoundError(source)
 filters=['setpts=PTS-STARTPTS','fps=30']
 if crop:
  x,y,w,h=crop
  # Clamp a crop that reaches the canvas footer to exclude the controls consistently.
  if y+h>1050:y=1050-h
  filters+=[f'crop={w}:{h}:{x}:{y}','scale=1920:1080:flags=lanczos']
 else:filters+=['scale=1920:1080:flags=lanczos']
 if caption:
  text=WORK/f'caption-{i}.txt';text.write_text(caption)
  filters += [f"drawtext=fontfile='{FONT}':textfile='{text}':fontsize=48:fontcolor=0xfff0ca:x=(w-text_w)/2:y=h-98:box=1:boxcolor=0x14251e@0.88:boxborderw=18"]
 filters += ['setsar=1','tpad=stop_mode=clone:stop_duration=0.3']
 out=WORK/f'{i:02}.mp4';encode(['-ss',str(start),'-i',str(source)],','.join(filters),dur,out);segments.append(out)
 timeline.append({'start':round(cursor,3),'duration':dur,'file':'source/'+source.name,'sourceStart':start,'crop':crop,'caption':caption});cursor+=dur
segments.append(title);timeline.append({'start':round(cursor,3),'duration':3,'file':'title-card.png','kind':'closing'});cursor+=3
concat=WORK/'concat.txt';concat.write_text(''.join("file '"+str(p).replace("'","'\\''")+"'\n" for p in segments))
run(['-f','concat','-safe','0','-i',str(concat),'-c','copy',str(WORK/'picture.mp4')])
AUDIO=ROOT/'Unity/BaraKitchen/Assets/BaraKitchen/Gameplay/Audio'
effects=[('chop',6.45,.7),('chop',7.15,.7),('chop',7.85,.7),('plate',10.1,.6),('success',12.6,.55),('guest-happy',14.0,.45),('guest-eat',15.5,.45),('wash',17.3,.5),('whoosh',24.9,.7),('whoosh',26.4,.6),('whoosh',28.4,.6),('warning',31.5,.25),('fire',34.5,.35),('spray',36.5,.5),('success',41.0,.45),('guest-happy',44.0,.35)]
inputs=['-i',str(WORK/'picture.mp4'),'-stream_loop','-1','-i',str(AUDIO/'music-service.wav')]
filters=[f'[1:a]aresample=48000,volume=0.65,atrim=0:{cursor},afade=t=in:d=0.7,afade=t=out:st={cursor-1.4}:d=1.4[music]'];labels=['[music]']
for i,(name,t,gain) in enumerate(effects,2):
 inputs+=['-i',str(AUDIO/(name+'.wav'))];filters.append(f'[{i}:a]aresample=48000,volume={gain},adelay={round(t*1000)}:all=1[s{i}]');labels.append(f'[s{i}]')
filters.append(''.join(labels)+f'amix=inputs={len(labels)}:duration=first:normalize=0,alimiter=limit=0.94,loudnorm=I=-16:TP=-2:LRA=9,aresample=48000,alimiter=limit=0.80:level=false[a]')
run([*inputs,'-filter_complex',';'.join(filters),'-map','0:v','-map','[a]','-c:v','copy','-c:a','aac','-b:a','192k','-ar','48000','-ac','2','-t',str(cursor),'-movflags','+faststart',str(OUT/'Bara-Kitchen-Trailer.mp4')])
(OUT/'edit.json').write_text(json.dumps({'duration':round(cursor,3),'resolution':[1920,1080],'fps':30,'source':'Actual Unity Web gameplay using keyboard input; crops and cuts only. Game music and foley mixed in post.','openingClosingIdentical':True,'timeline':timeline,'effects':effects},indent=2))
run(['-ss','15','-i',str(OUT/'Bara-Kitchen-Trailer.mp4'),'-frames:v','1',str(OUT/'poster.jpg')])
print('BARA_TRAILER_RENDER_OK seconds='+str(round(cursor,3)),flush=True)
