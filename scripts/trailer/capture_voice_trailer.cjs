// Actual Unity controls + actual GPT-Live replies. The player microphone is synthetic for this recording.
const fs=require('fs'),path=require('path'),{chromium}=require('../browser-deps.cjs')('playwright'),driver=require('../gameplay/driver.cjs');
const mode=process.argv[2]||'conversation',level=mode==='pot'?2:1,out=path.resolve('art/trailers/round-02'),source=path.join(out,'source');
const questions=JSON.parse(fs.readFileSync(path.join(source,'questions.json'))).questions;
(async()=>{
 const browser=await chromium.launch({executablePath:process.env.BARA_CHROME,headless:true,args:['--enable-unsafe-swiftshader','--autoplay-policy=no-user-gesture-required']});
 const ctx=await browser.newContext({viewport:{width:1920,height:1080}}),page=await ctx.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
 const clips=[];
 try{
  await page.addInitScript(()=>{
   const AC=window.AudioContext||window.webkitAudioContext,ac=new AC({sampleRate:48000});const mic=ac.createMediaStreamDestination(),mix=ac.createMediaStreamDestination(),analyser=ac.createAnalyser();analyser.fftSize=512;
   const quiet=ac.createConstantSource();quiet.offset.value=0;quiet.connect(mic);quiet.start();
   navigator.mediaDevices.getUserMedia=async()=>{await ac.resume();return mic.stream};
   const NativeAudio=window.Audio;window.Audio=function(...args){const a=new NativeAudio(...args);a.muted=true;return a};window.Audio.prototype=NativeAudio.prototype;
   const RTC=window.RTCPeerConnection;window.RTCPeerConnection=class extends RTC{constructor(...args){super(...args);this.addEventListener('track',e=>{const s=ac.createMediaStreamSource(new MediaStream([e.track]));s.connect(mix);s.connect(analyser);window.voiceCapture.remote=s})}};
   window.voiceCapture={ac,mic,mix,analyser,events:[],lastVoice:0,started:0};
   window.baraLiveTestEvent=e=>{if(e.type==='session.input_transcript.delta'||e.type==='session.output_transcript.delta'){const c=voiceCapture,t=performance.now();if(e.type==='session.output_transcript.delta')c.lastVoice=t;c.events.push({type:e.type,delta:e.delta,at:t,start_ms:e.start_ms,end_ms:e.end_ms})}};
  });
  await page.route('**/live-chat.js*',async r=>{const content=fs.readFileSync('art/production/gameplay-round-01/live-chat.js','utf8').replace('http://127.0.0.1:54115','http://127.0.0.1:54116');await r.fulfill({contentType:'application/javascript',body:content})});
  await page.goto('http://127.0.0.1:54114/production/gameplay-round-01/?v=voice-trailer');const g=driver(page,level);await g.boot();
  async function start(name){await page.evaluate(name=>{const c=voiceCapture;c.events=[];c.started=performance.now();c.name=name;c.chunks=[];c.video=document.querySelector('canvas').captureStream(30);c.recorder=new MediaRecorder(new MediaStream([...c.video.getVideoTracks(),...c.mix.stream.getAudioTracks()]),{mimeType:'video/webm;codecs=vp8,opus',videoBitsPerSecond:10000000,audioBitsPerSecond:192000});c.recorder.ondataavailable=e=>{if(e.data.size)c.chunks.push(e.data)};c.recorder.start(200)},name);console.log('RECORD',name)}
  async function stop(name,extra={}){const data=await page.evaluate(()=>new Promise(resolve=>{const c=voiceCapture;c.recorder.onstop=async()=>{const bytes=new Uint8Array(await new Blob(c.chunks,{type:'video/webm'}).arrayBuffer());let b='';for(let i=0;i<bytes.length;i+=32768)b+=String.fromCharCode(...bytes.subarray(i,i+32768));resolve({base64:btoa(b),seconds:(performance.now()-c.started)/1000,events:c.events.map(e=>({...e,at:(e.at-c.started)/1000}))});c.video.getTracks().forEach(t=>t.stop())};c.recorder.stop()}));fs.writeFileSync(path.join(source,name+'.webm'),Buffer.from(data.base64,'base64'));delete data.base64;const report={name,...data,...extra,state:await g.state()};clips.push(report);fs.writeFileSync(path.join(source,name+'.json'),JSON.stringify(report,null,2)+'\n');await page.screenshot({path:path.join(out,'qa',name+'.png')});console.log('SAVED',name,data.seconds.toFixed(2));return report}
  async function quiet(min=2,max=20000){const began=Date.now();while(Date.now()-began<max){const ready=await page.evaluate(min=>{const c=voiceCapture,a=new Float32Array(c.analyser.fftSize);c.analyser.getFloatTimeDomainData(a);const rms=Math.sqrt(a.reduce((s,v)=>s+v*v,0)/a.length);return c.lastVoice>0&&performance.now()-c.lastVoice>min*1000&&rms<.008},min);if(ready)return;await g.sleep(250)}}
  async function chat(){await start('chat-button');await g.click('AI chat · V');await page.waitForFunction(()=>window.baraState.helpState==='connected',null,{timeout:40000});await g.sleep(1000);await stop('chat-button');await quiet(2,20000)}
  async function say(name,expected,after){await start(name);await g.sleep(500);const base64=fs.readFileSync(path.join(source,name+'-question.wav')).toString('base64');await page.evaluate(async({base64,text})=>{const c=voiceCapture;await c.ac.resume();const bytes=Uint8Array.from(atob(base64),x=>x.charCodeAt(0));const b=await c.ac.decodeAudioData(bytes.buffer);const s=c.ac.createBufferSource();s.buffer=b;s.connect(c.mic);s.connect(c.mix);c.question={text,start:(performance.now()-c.started)/1000,duration:b.duration};s.start()}, {base64,text:questions[name]});
   const began=Date.now();let transcript='';while(Date.now()-began<40000){transcript=await page.evaluate(()=>voiceCapture.events.filter(e=>e.type==='session.output_transcript.delta').map(e=>e.delta).join(''));if(Date.now()-began>4000&&expected.test(transcript)){await quiet(2.5,18000);break}await g.sleep(250)}
   if(!expected.test(transcript))throw Error('No relevant voice reply for '+name+': '+transcript);console.log('REPLY',name,transcript);
   const question=await page.evaluate(()=>voiceCapture.question);if(after)await after();await g.sleep(600);return stop(name,{question,assistant:await page.evaluate(()=>voiceCapture.events.filter(e=>e.type==='session.output_transcript.delta').map(e=>e.delta).join(''))})}
  if(mode==='ending'){
   await g.go('C3');await g.orbit(-20);await page.keyboard.down('s');await g.sleep(130);await page.keyboard.up('s');await g.sleep(500);await chat();await say('cutest',/bara|capybara|me\b/i);
  }else if(mode==='conversation'){
   await g.use('dishes');await g.use(g.assembly);await g.use('tomato');await g.go('prep');await g.orbit(35);await chat();
   await say('chop',/space|chopping board/i,async()=>{await g.press('e');await g.press('Space');await page.waitForFunction(()=>window.baraState.stations.find(s=>s.id==='prep').item.startsWith('Chopped'),null,{timeout:10000});await g.sleep(650)});
   await g.press('e');await g.use(g.assembly);await g.chop('cucumber');await g.use(g.assembly);await g.use(g.assembly);await g.serve();
   await say('progress',/served|point|score|streak|dish|order/i);
   await say('casual',/./s,async()=>{await g.orbit(20)});
   await g.go('C3');await g.orbit(-20);await page.keyboard.down('s');await g.sleep(130);await page.keyboard.up('s');await g.sleep(500);await say('cutest',/bara|capybara|me\b/i);
  }else{
   await g.loadSoup();await page.waitForFunction(()=>window.baraState.stations.find(s=>s.id==='pot').heat==='Ready',null,{timeout:20000});await g.press('e');await g.serve();
   // Start the live companion before cooking another genuine batch.
   await chat();await g.loadSoup();await g.orbit(45);await g.sleep(600);await start('pot-reminder');
   const began=Date.now();let spoken='';while(Date.now()-began<35000){spoken=await page.evaluate(()=>voiceCapture.events.filter(e=>e.type==='session.output_transcript.delta').map(e=>e.delta).join(''));const pot=(await g.state()).stations.find(s=>s.id==='pot');if(/ready|burn|pot|soup/i.test(spoken)&&['Ready','Warning'].includes(pot.heat)){await quiet(.8,4000);await g.press('e');break}if(pot.heat==='Burning'&&/fire|burning|extinguisher/i.test(spoken))break;await g.sleep(250)}
   if(!/ready|burn|pot|soup/i.test(spoken))throw Error('No proactive pot reminder');await g.sleep(700);await stop('pot-reminder',{assistant:await page.evaluate(()=>voiceCapture.events.filter(e=>e.type==='session.output_transcript.delta').map(e=>e.delta).join(''))});
  }
  await page.keyboard.press('v');await page.waitForFunction(()=>window.baraState.helpState==='off',null,{timeout:16000});
  if(errors.length)throw Error(errors.join('\n'));fs.writeFileSync(path.join(out,'qa',mode+'-capture.json'),JSON.stringify({passed:true,model:'gpt-live-1',voice:'marin',syntheticPlayer:true,assistantReplies:'Actual live session output',controlsOnly:true,clips:clips.map(c=>({name:c.name,seconds:c.seconds})),errors},null,2)+'\n');
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
