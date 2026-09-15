"""Render the voice trailer from real Unity/WebRTC captures, preserving the recorded replies."""
import json,os,shutil,subprocess,textwrap
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];os.chdir(ROOT)
OUT=ROOT/'art/trailers/round-02';SRC=OUT/'source';WORK=ROOT/'.local/voice-trailer';WORK.mkdir(parents=True,exist_ok=True)
FF=os.environ.get('FFMPEG_BIN') or shutil.which('ffmpeg') or str(ROOT/'.local/audio-tools/imageio_ffmpeg/binaries/ffmpeg-macos-aarch64-v7.1')
FONTS=ROOT/'Unity/BaraKitchen/Assets/BaraKitchen/Gameplay/UI/Fonts';FONT=FONTS/'LilitaOne.ttf'
def run(args):subprocess.run([FF,'-hide_banner','-loglevel','error','-y',*map(str,args)],check=True)
def ass_time(t):return f'{int(t)//3600}:{int(t)//60%60:02}:{t%60:05.2f}'
def ass(path,lines):
 header='''[Script Info]
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080
WrapStyle: 2
[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Caption,Varela Round,44,&H00DEF0FF,&H00DEF0FF,&H00142112,&H00142112,0,0,0,0,100,100,0,0,1,0,0,8,100,100,0,1
[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
'''
 for start,end,role,body in lines:
  body=body.replace('\n','\\N');color='&HBDE6CE&' if role=='BARA' else '&HDEF0FF&'
  text='{\\an8\\pos(960,922)\\fs25\\c'+color+'}'+role+'{\\fs44\\c&HDEF0FF&}\\N'+body
  header+=f'Dialogue: 0,{ass_time(start)},{ass_time(end)},Caption,,0,0,0,,{text}\n'
 path.write_text(header)
# Cuts remove waiting/backchannels but never replace the live assistant audio.
shots=[
 {'name':'chat-button','start':0,'end':3.1,'wide':True,'feature':'One tap. A little company.','mute':True,'captions':[]},
 {'name':'chop','start':.35,'end':4.45,'crop':[460,220,1120,525],'feature':'Ask while you cook','captions':[(.1,4.05,'YOU','Bara, how do I chop this tomato?')]},
 {'name':'chop','start':7.0,'end':15.6,'crop':[460,220,1120,525],'feature':'Ask while you cook','captions':[(.2,5.25,'BARA','Pop it onto the chopping board with E, then press\nSpace with empty paws to chop.')]},
 {'name':'progress','start':.35,'end':4.1,'wide':True,'feature':'A little encouragement','captions':[(.1,3.7,'YOU','How am I doing so far?')]},
 {'name':'progress','start':7.25,'end':15.8,'wide':True,'feature':'A little encouragement','captions':[(.1,5.9,'BARA',"Nice pace! You're at 80 points with one served,\nnone missed, and a one-hit streak."),(5.9,8.5,'BARA','Only 20 more points for the first star.')]},
 {'name':'pot-reminder','start':16.2,'end':22.5,'crop':[560,330,1024,480],'feature':'A timely nudge','captions':[(.7,6.2,'BARA','Ooh, quick one: the soup is ready. Scoop it with\na clean empty plate before it burns.')]},
 {'name':'casual','start':.35,'end':9.08,'crop':[530,325,1152,540],'feature':'Or just have a little chat','captions':[(.1,3.4,'YOU','This little café feels cozy, huh?'),(3.65,8.7,'BARA','Oh, super cozy! Like, tiny mug\nand warm soup energy.')]},
 {'name':'cutest','start':.35,'end':7.85,'crop':[570,145,896,420],'feature':'Your new kitchen companion','captions':[(.1,3.9,'YOU','What is the cutest animal in this kitchen?'),(4.1,7.5,'BARA','Hee-hee, me! Bara, the capybara.')]},
]
common=['-r','30','-c:v','libx264','-preset','fast','-crf','18','-maxrate','10M','-bufsize','20M','-pix_fmt','yuv420p','-color_primaries','bt709','-color_trc','bt709','-colorspace','bt709','-color_range','tv','-c:a','aac','-b:a','192k','-ar','48000','-ac','2','-movflags','+faststart']
title=WORK/'title.mp4'
run(['-loop','1','-framerate','30','-i',OUT/'title-card.png','-f','lavfi','-i','anullsrc=r=48000:cl=stereo','-vf',"scale=3840:2160,zoompan=z='1+on*0.00038':x='iw/2-iw/zoom/2':y='ih/2-ih/zoom/2':d=1:s=1920x1080:fps=30,setsar=1",'-t','3',*common,title])
segments=[title];timeline=[{'start':0,'duration':3,'kind':'title','file':'title-card.png'}];cursor=3
for i,s in enumerate(shots):
 dur=round((s['end']-s['start'])*30)/30
 captions=WORK/f'caption-{i}.ass';ass(captions,s['captions'])
 feature=WORK/f'feature-{i}.txt';feature.write_text(s['feature'])
 filters=['setpts=PTS-STARTPTS','fps=30']
 if s.get('wide'):filters+=['scale=1600:900:flags=lanczos','pad=1920:1080:160:0:color=0x14251e']
 else:
  x,y,w,h=s['crop'];filters+=[f'crop={w}:{h}:{x}:{y}','scale=1920:900:flags=lanczos','pad=1920:1080:0:0:color=0x14251e']
 filters += [f"drawtext=fontfile='{FONT}':textfile='{feature}':fontsize=40:fontcolor=0xfff0ca:x=42:y=26:box=1:boxcolor=0x14251e@0.88:boxborderw=14",f"subtitles='{captions}':fontsdir='{FONTS}'",'setsar=1','setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709']
 # Balance the synthetic player against the softer live voice; actual reply waveform is retained.
 q=json.loads((SRC/(s['name']+'.json')).read_text()).get('question')
 af=['asetpts=PTS-STARTPTS','aresample=48000']
 if s.get('mute'):af+=['volume=0']
 else:
  if q:
   a=q['start']-s['start'];b=a+q['duration']
   if b>0:af += [f"volume='if(between(t,{max(0,a):.3f},{b:.3f}),0.45,1)':eval=frame"]
  af+=['loudnorm=I=-17:TP=-2:LRA=8','aresample=48000','afade=t=in:d=0.025',f'afade=t=out:st={dur-.04}:d=0.04']
 dst=WORK/f'{i:02}.mp4';run(['-ss',s['start'],'-i',SRC/(s['name']+'.webm'),'-vf',','.join(filters),'-af',','.join(af),'-t',dur,*common,dst]);segments.append(dst)
 timeline.append({'start':round(cursor,3),'duration':dur,'sourceStart':s['start'],'sourceEnd':s['end'],'file':'source/'+s['name']+'.webm','crop':s.get('crop'),'feature':s['feature'],'captions':s['captions']});cursor+=dur
segments.append(title);timeline.append({'start':round(cursor,3),'duration':3,'kind':'title','file':'title-card.png'});cursor+=3
concat=WORK/'concat.txt';concat.write_text(''.join("file '"+str(p).replace("'","'\\''")+"'\n" for p in segments))
run(['-f','concat','-safe','0','-i',concat,'-c','copy',WORK/'dialogue-edit.mp4'])
music=ROOT/'Unity/BaraKitchen/Assets/BaraKitchen/Gameplay/Audio/music-service.wav'
filters=f'[0:a]aresample=48000[voice];[1:a]aresample=48000,volume=0.18,atrim=0:{cursor},afade=t=in:d=1,afade=t=out:st={cursor-1}:d=1[music];[voice][music]amix=inputs=2:normalize=0:duration=first,alimiter=limit=0.84:level=false[a]'
run(['-i',WORK/'dialogue-edit.mp4','-stream_loop','-1','-i',music,'-filter_complex',filters,'-map','0:v','-map','[a]','-c:v','copy','-c:a','aac','-b:a','192k','-ar','48000','-ac','2','-t',cursor,'-movflags','+faststart',OUT/'Bara-Kitchen-Voice-Trailer.mp4'])
(OUT/'edit.json').write_text(json.dumps({'duration':round(cursor,3),'resolution':[1920,1080],'fps':30,'openingClosingIdentical':True,'source':'Actual Unity keyboard gameplay and GPT-Live WebRTC replies. Player questions are synthetic. Pauses/backchannels shortened; crops, captions, loudness balancing and game music added in editing. Closing joke follows the authored Bara persona. No fabricated game scores or model replies.','model':'gpt-live-1','voice':'marin','timeline':timeline},indent=2)+'\n')
run(['-ss',cursor-7,'-i',OUT/'Bara-Kitchen-Voice-Trailer.mp4','-frames:v','1',OUT/'poster.jpg'])
print('VOICE_TRAILER_RENDER_OK',round(cursor,3),'seconds',flush=True)
