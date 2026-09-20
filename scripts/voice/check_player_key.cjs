// Paid opt-in test: actual password popup + a keyless helper + synthetic microphone.
const fs=require('fs'),path=require('path'),{chromium}=require('../browser-deps.cjs')('playwright');
if(!process.argv.includes('--live'))throw Error('Pass --live for the synthetic API check.');
const key=process.env.OPENAI_API_KEY||fs.readFileSync('.env','utf8').split('\n').find(l=>l.startsWith('OPENAI_API_KEY=')).split('=').slice(1).join('=').trim().replace(/^['"]|['"]$/g,'');
(async()=>{
 const browser=await chromium.launch({executablePath:process.env.BARA_CHROME,headless:true,args:['--enable-unsafe-swiftshader','--autoplay-policy=no-user-gesture-required','--use-fake-device-for-media-stream','--use-file-for-fake-audio-capture='+path.resolve('scripts/voice/fixtures/browser-question.wav')]});
 const context=await browser.newContext({viewport:{width:1600,height:1000},permissions:['microphone']}),page=await context.newPage(),checks=[],errors=[];
 const out='art/production/gameplay-round-01/qa/voice';
 const check=(ok,label)=>{if(!ok)throw Error(label);checks.push(label);console.log('PASS '+label)};
 try{
  page.on('pageerror',e=>errors.push(e.message));
  await page.addInitScript(()=>{window.keyQA={mic:[],output:'',sentKey:false};const original=navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);navigator.mediaDevices.getUserMedia=async o=>{const s=await original(o);keyQA.mic.push(...s.getTracks());return s};window.baraLiveTestEvent=e=>{if(e.type==='session.output_transcript.delta')keyQA.output+=e.delta};});
  await page.route('**/live-chat.js*',r=>r.fulfill({contentType:'application/javascript',body:fs.readFileSync('art/production/gameplay-round-01/live-chat.js','utf8').replace('http://127.0.0.1:54115','http://127.0.0.1:54116')}));
  await page.goto('http://127.0.0.1:54114/production/gameplay-round-01/?v=player-key-01');
  await page.waitForFunction(()=>window.baraState?.phase==='Menu',null,{timeout:120000});
  const state=()=>page.evaluate(()=>window.baraState);
  async function at(b){const box=await page.locator('canvas').boundingBox();await page.mouse.click(box.x+(b.x+b.w/2)*box.width,box.y+(b.y+b.h/2)*box.height);await page.waitForTimeout(350)}
  async function click(label){const b=(await state()).buttons.find(b=>b.label===label);if(!b)throw Error('Missing button '+label);await at(b)}
  async function enter(value){await page.locator('input[aria-label="OpenAI API key"]').fill(value);await page.waitForTimeout(300)}
  const health=await page.request.get('http://127.0.0.1:54116/health');check((await health.json()).keyRequired,'Helper starts with no environment or developer key');
  await click('Begin service');await click('Open the cafe');await click('AI chat · V');
  check((await state()).helpState==='setup','First Chat opens key setup');
  check(await page.evaluate(()=>keyQA.mic.length===0),'No microphone before key confirmation');
  const before=await state();await enter('invalid-key-wasdcv');const after=await state();
  check(Math.hypot(before.x-after.x,before.z-after.z)<.001&&before.remaining===after.remaining&&before.cameraYaw===after.cameraYaw,'Typing does not move chef, rotate camera, or advance timers');
  await click('Connect & chat');check((await state()).helpState==='setup'&&(await state()).visibleLabels.some(l=>l.includes('valid OpenAI key')),'Invalid key stays in setup with a clear error');
  await click('Not now');check((await state()).helpState==='off','Cancel returns to ordinary gameplay');
  await click('AI chat · V');await enter(key);
  check(!JSON.stringify(await state()).includes(key),'Password value excluded from game telemetry');
  await page.screenshot({path:path.join(out,'player-key-popup.png')});
  await click('Connect & chat');await page.waitForFunction(()=>window.baraState.helpState==='connected',null,{timeout:45000});
  check(true,'Player-entered key connects to real GPT-Live through keyless helper');
  check((await state()).phase==='Service','Connecting restores gameplay');
  await page.waitForTimeout(24000);
  check(await page.evaluate(()=>/tomato|cucumber|chop/i.test(keyQA.output)),'Delegated cooking help uses the player key too');
  await page.keyboard.press('v');await page.waitForTimeout(200);check(await page.evaluate(()=>keyQA.mic.every(t=>t.readyState==='ended')),'End chat stops microphone capture');
  await page.waitForFunction(()=>window.baraState.helpState==='off',null,{timeout:16000});
  await click('Pause');await click('Settings');await click('Voice setup');await click('Forget key');
  check((await state()).visibleLabels.some(l=>l.startsWith('Key forgotten')),'Forget key clears the session credential');
  await click('Connect & chat');check((await state()).helpState==='setup','Forgotten key cannot reconnect');
  await click('Not now');check((await state()).phase==='Paused','Settings setup preserves prior pause');
  const saved=await page.evaluate(()=>JSON.stringify(Object.entries(localStorage)));check(!saved.includes(key),'Key not written to browser local storage');
  await page.reload();await page.waitForFunction(()=>window.baraState?.phase==='Menu',null,{timeout:120000});await click('AI chat · V');await click('Connect & chat');check((await state()).helpState==='setup','Reload starts with an empty key');
  check(errors.length===0,'No browser errors');
  fs.writeFileSync(path.join(out,'player-key-validation.json'),JSON.stringify({passed:true,model:'gpt-live-1',syntheticMicrophoneOnly:true,checks,errors},null,2)+'\n');
 }finally{await browser.close()}
})().catch(e=>{console.error(e.message);process.exitCode=1});
