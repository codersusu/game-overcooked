// Paid opt-in GPT-Live + real Unity UI, using synthetic microphone audio only.
const fs=require('fs'),path=require('path'),deps=require('../browser-deps.cjs');const {chromium}=deps('playwright');
if(!process.argv.includes('--live'))throw Error('Pass --live for synthetic paid API checks.');
const key=process.env.OPENAI_API_KEY||fs.readFileSync('.env','utf8').split('\n').find(l=>l.startsWith('OPENAI_API_KEY=')).split('=').slice(1).join('=').trim().replace(/^['"]|['"]$/g,'');
(async()=>{
 const out=path.resolve('art/production/gameplay-round-01/qa/voice');
 const browser=await chromium.launch({executablePath:process.env.BARA_CHROME,headless:true,args:['--enable-unsafe-swiftshader','--autoplay-policy=no-user-gesture-required','--use-fake-device-for-media-stream','--use-file-for-fake-audio-capture='+path.resolve('scripts/voice/fixtures/browser-question.wav')]});
 const checks=[],errors=[];
 try{
  const context=await browser.newContext({viewport:{width:1600,height:1000},permissions:['microphone']});
  await context.addInitScript(()=>{
   window.qaLive={events:[],tracks:[],pcs:[],stops:[],targets:[]};window.baraLiveTestEvent=e=>{if(e.type!=='session.output_audio.delta')window.qaLive.events.push(e)};
   const gum=navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);navigator.mediaDevices.getUserMedia=async o=>{const s=await gum(o);window.qaLive.tracks.push(...s.getTracks());return s};
   const RTC=window.RTCPeerConnection;window.RTCPeerConnection=class extends RTC{constructor(...a){super(...a);window.qaLive.pcs.push(this)}};
   const fetchOriginal=window.fetch;window.fetch=async(...args)=>{const r=await fetchOriginal(...args);if(String(args[0]).endsWith('/api/live/stop'))r.clone().json().then(x=>window.qaLive.stops.push(x));return r};
  });
  const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:54114/production/gameplay-round-01/?v=gpt-live-01');
  await page.waitForFunction(()=>window.baraState?.phase==='Menu',null,{timeout:120000});
  const state=()=>page.evaluate(()=>window.baraState);const check=(ok,label)=>{if(!ok)throw Error(label);checks.push(label);console.log('PASS '+label)};
  const click=async label=>{const b=(await state()).buttons.find(b=>b.label===label);if(!b)throw Error('Button not found '+label);const box=await page.locator('canvas').boundingBox();await page.mouse.click(box.x+(b.x+b.w/2)*box.width,box.y+(b.y+b.h/2)*box.height);await page.waitForTimeout(350)};
  async function startChat(){await click('AI chat · V');if((await state()).helpState==='setup'){await page.locator('input[aria-label="OpenAI API key"]').fill(key);await click('Connect & chat');}}
  await click('Begin service');await click('Open the cafe');await page.waitForTimeout(3500);
  check(await page.evaluate(()=>qaLive.tracks.length===0),'No microphone requested before Chat');
  await startChat();
  await page.waitForFunction(()=>window.baraState.helpState==='connected'||window.baraState.visibleLabels.some(x=>x.includes('could not')||x.includes('denied')||x.includes('timed out')),null,{timeout:45000});
  console.log('Connect',JSON.stringify({help:(await state()).helpState,labels:(await state()).visibleLabels.slice(-8)}));
  check((await state()).helpState==='connected','Real GPT-Live WebRTC connects from the game button');
  check((await state()).phase==='Service','Live chat leaves service running');
  const before=await state();await page.keyboard.down('s');await page.waitForTimeout(500);await page.keyboard.up('s');const after=await state();check(Math.hypot(before.x-after.x,before.z-after.z)>.1,'Movement works while microphone is on');
  check(!(await state()).buttons.some(b=>['Ask','Talk to chef','Finish & ask','Replay'].includes(b.label)),'Only live chat control; no writing or recording panel');
  await page.screenshot({path:path.join(out,'live-chat.png')});
  await page.waitForTimeout(24000);
  const transcripts=await page.evaluate(()=>({input:qaLive.events.filter(e=>e.type==='session.input_transcript.delta').map(e=>e.delta).join(''),output:qaLive.events.filter(e=>e.type==='session.output_transcript.delta').map(e=>e.delta).join('')}));
  console.log('Speech',JSON.stringify(transcripts));check(/salad/i.test(transcripts.input),'Synthetic spoken gameplay question is heard');check(/tomato|cucumber|chop/i.test(transcripts.output),'Chef speaks contextual cooking guidance');
  const inbound=await page.evaluate(async()=>{let bytes=0;for(const pc of qaLive.pcs){for(const r of (await pc.getStats()).values())if(r.type==='inbound-rtp'&&r.kind==='audio')bytes+=r.bytesReceived||0;}return bytes});check(inbound>1000,'Assistant audio is received over WebRTC');
  await page.screenshot({path:path.join(out,'live-guidance.png')});
  await page.keyboard.press('v');await page.waitForTimeout(200);check(await page.evaluate(()=>qaLive.tracks.every(t=>t.readyState==='ended')),'End chat immediately stops microphone tracks');
  await page.waitForFunction(()=>window.baraState.helpState==='off',null,{timeout:16000});check((await state()).phase==='Service','Ending chat leaves the kitchen playable');
  const stops=await page.evaluate(()=>qaLive.stops);check(stops.some(s=>s.finalized),'Live session closes with confirmed final usage');
  // Offline and denied permission are deliberate failures; neither may pause the game.
  await page.route('**/health',r=>r.abort());await startChat();await page.waitForTimeout(1200);check((await state()).helpState==='off'&&(await state()).phase==='Service','Offline helper fails gracefully without pausing');await page.unroute('**/health');
  await context.clearPermissions();await startChat();await page.waitForTimeout(1500);check((await state()).visibleLabels.some(x=>x.includes('permission denied')),'Denied microphone permission has a clear small status');check((await state()).phase==='Service','Denied permission leaves normal gameplay available');
  check(errors.length===0,'No browser page errors');
  fs.writeFileSync(path.join(out,'browser-live.json'),JSON.stringify({passed:true,model:'gpt-live-1',syntheticAudioOnly:true,checks,transcripts,inboundAudioBytes:inbound,finalUsage:stops.filter(s=>s.finalized).map(s=>s.seconds),pageErrors:errors},null,2)+'\n');
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
