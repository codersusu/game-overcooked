// Real keyboard playthrough plus short, unmodified Unity canvas captures.
const fs=require('fs'),path=require('path');
const deps=require('../browser-deps.cjs');
const {chromium}=deps('playwright');
const level=Number(process.argv[2]||1),capture=process.env.BARA_CAPTURE!=='0',potRetake=process.env.BARA_TRAILER_POT_RETAKE==='1';
const layout=JSON.parse(fs.readFileSync('art/concepts/levels-round-02/levels.json'))[level-1],moduleSize=.92;
const qa=path.resolve(potRetake?'art/trailers/round-01/qa/pot-diagonal':'art/production/gameplay-round-01/qa/full-playthrough'),raw=path.resolve('.local/trailer-capture');
fs.mkdirSync(qa,{recursive:true});fs.mkdirSync(raw,{recursive:true});
const checks=[],clips=[],snapshots={},errors=[];let browser,page,blocked;
const state=()=>page.evaluate(()=>window.baraState);
const check=(ok,label)=>{if(!ok)throw Error(label);checks.push(label);console.log('L'+level+' PASS '+label)};
const sleep=ms=>page.waitForTimeout(ms);
async function screenshot(name){await page.screenshot({path:path.join(qa,'level-'+level+'-'+name+'.png')})}
async function click(label,n=0){const b=(await state()).buttons.filter(x=>x.label===label)[n];if(!b)throw Error('Missing button '+label);const c=await page.locator('canvas').boundingBox();await page.mouse.click(c.x+(b.x+b.w/2)*c.width,c.y+(b.y+b.h/2)*c.height);await sleep(600)}
const world=(x,y)=>({x:((layout.cols-1)/2-x)*moduleSize,z:y*moduleSize});
const cell=s=>({x:Math.round((layout.cols-1)/2-s.x/moduleSize),y:Math.round(s.z/moduleSize)});
const key=p=>p.x+','+p.y;
const free=p=>p.x>=0&&p.x<layout.cols&&p.y>=0&&p.y<layout.rows&&!blocked.has(key(p));
function route(a,b){const queue=[a],prev=new Map([[key(a),null]]);for(let n=0;n<queue.length;n++){let p=queue[n];if(key(p)===key(b)){let r=[];while(p){r.push(p);p=prev.get(key(p))}r.reverse();return r.filter((p,i)=>i===0||i===r.length-1||(r[i+1].x-r[i].x!==r[i].x-r[i-1].x||r[i+1].y-r[i].y!==r[i].y-r[i-1].y))}for(const d of [[1,0],[-1,0],[0,1],[0,-1]]){const q={x:p.x+d[0],y:p.y+d[1]};if(free(q)&&!prev.has(key(q))){prev.set(key(q),p);queue.push(q)}}}return null}
function direction(s,dx,dz){const r=dx*s.rightX+dz*s.rightZ,f=dx*s.forwardX+dz*s.forwardZ,m=Math.max(Math.abs(r),Math.abs(f));const a=[];if(Math.abs(r)>m*.42)a.push(r>0?'d':'a');if(Math.abs(f)>m*.42)a.push(f>0?'w':'s');return a}
async function push(keys,ms){for(const k of keys)await page.keyboard.down(k);await sleep(ms);for(const k of keys)await page.keyboard.up(k);await sleep(175)}
async function travel(target){for(let i=0;i<22;i++){const s=await state();if(s.phase!=='Service')throw Error('Service ended while travelling');const dx=target.x-s.x,dz=target.z-s.z,d=Math.hypot(dx,dz);if(d<.13)return;await push(direction(s,dx,dz),Math.max(25,Math.min(650,d/3.15*970)))}throw Error('Stuck travelling '+JSON.stringify({target,state:await state()}))}
async function goCell(goal){const r=route(cell(await state()),goal);if(!r)throw Error('No route to cell '+key(goal));for(const p of r)await travel(world(p.x,p.y))}
async function face(point){const s=await state();await push(direction(s,point.x-s.x,point.z-s.z),42)}
async function go(id){const s=await state(),t=s.stations.find(x=>x.id===id);if(!t)throw Error('No station '+id);const g=cell(t);const options=[[1,0],[-1,0],[0,1],[0,-1]].map(d=>({x:g.x+d[0],y:g.y+d[1]})).filter(free).map(p=>({p,r:route(cell(s),p)})).filter(x=>x.r).sort((a,b)=>a.r.reduce((v,p,i)=>v+(i?Math.abs(p.x-a.r[i-1].x)+Math.abs(p.y-a.r[i-1].y):0),0)-b.r.reduce((v,p,i)=>v+(i?Math.abs(p.x-b.r[i-1].x)+Math.abs(p.y-b.r[i-1].y):0),0));if(!options.length)throw Error('No approach '+id);for(const p of options[0].r)await travel(world(p.x,p.y));await face(t);if((await state()).focused!==id){await sleep(200);if((await state()).focused!==id)throw Error('Could not focus '+id+' '+JSON.stringify(await state()))}}
async function press(k){await page.keyboard.press(k);await sleep(220)}
async function use(id){await go(id);await press('e');console.log('L'+level+' USE '+id+' '+(await state()).held)}
async function startClip(name){if(!capture)return;await page.evaluate(()=>{window.__baraChunks=[];const stream=document.querySelector('canvas').captureStream(30);window.__baraCaptureStream=stream;window.__baraRecorder=new MediaRecorder(stream,{mimeType:'video/webm;codecs=vp8',videoBitsPerSecond:10000000});window.__baraRecorder.ondataavailable=e=>{if(e.data.size)window.__baraChunks.push(e.data)};window.__baraRecordStart=performance.now();window.__baraRecorder.start(200)});console.log('L'+level+' RECORD '+name)}
async function stopClip(name){if(!capture)return;const result=await page.evaluate(()=>new Promise(resolve=>{const rec=window.__baraRecorder;rec.onstop=async()=>{const bytes=new Uint8Array(await new Blob(window.__baraChunks,{type:'video/webm'}).arrayBuffer());let binary='';for(let i=0;i<bytes.length;i+=32768)binary+=String.fromCharCode(...bytes.subarray(i,i+32768));resolve({base64:btoa(binary),seconds:(performance.now()-window.__baraRecordStart)/1000});window.__baraCaptureStream.getTracks().forEach(t=>t.stop())};rec.stop()}));const file='level-'+level+'-'+name+'.webm';fs.writeFileSync(path.join(raw,file),Buffer.from(result.base64,'base64'));clips.push({file,seconds:result.seconds,level});fs.writeFileSync(path.join(raw,'level-'+level+'-clips.json'),JSON.stringify(clips,null,2))}
async function shot(name,fn){await startClip(name);await fn();await stopClip(name)}
async function chopHeld(film=false){await use('prep');check((await state()).stations.find(s=>s.id==='prep').item&&!((await state()).held),'Ingredient placed on cutting board');const action=async()=>{await sleep(250);await press('Space');await page.waitForFunction(()=>window.baraState.stations.find(s=>s.id==='prep').item.startsWith('Chopped'),{},{timeout:12000});await sleep(550)};if(film)await shot('chopping',action);else await action();await press('e');check((await state()).held.startsWith('Chopped'),'Chopping completes from one key press')}
async function chop(id,film=false){await use(id);await chopHeld(film)}
async function throwHeld(){const target=level===3?'C4':'C5',origin=level===3?{x:4,y:3}:{x:3,y:2};await goCell(origin);const t=(await state()).stations.find(s=>s.id===target);await face(t);await shot('throwing',async()=>{await sleep(300);await press('q');await page.waitForFunction(id=>window.baraState.stations.find(s=>s.id===id).item!==''&&window.baraState.held==='',target,{timeout:8000});await sleep(800)});check((await state()).stations.find(s=>s.id===target).item!=='','Ingredient thrown across a rail onto a counter');await use(target)}
const assembly=level===1?'C1':level===2?'C2':level===3?'C5':'C7';
async function salad(film=false){await use('dishes');await use(assembly);await use('tomato');if(film&&level===4)await throwHeld();await chopHeld(film);if(film&&level===3)await throwHeld();await use(assembly);await chop('cucumber');await go(assembly);const combine=async()=>{await press('e');await sleep(650);check((await state()).stations.find(s=>s.id===assembly).recipe==='salad','Prepared tomato and cucumber become salad');await press('e');await sleep(650)};if(film)await shot('assemble-salad',combine);else await combine()}
async function soup(film=false,burn=false){await use('dishes');await use(assembly);await chop('carrot',film);await use(assembly);await chop('mushroom');await use(assembly);check(!(await state()).stations.find(s=>s.id===assembly).recipe,'Uncooked carrot and mushroom remain ingredients on the plate');await use(assembly);await use('pot');check((await state()).held==='Clean dish'&&(await state()).stations.find(s=>s.id==='pot').heat==='Cooking','Plate tips prepared ingredients into the pot');if(film)await shot('cooking',async()=>{await sleep(4500)});await page.waitForFunction(()=>window.baraState.stations.find(s=>s.id==='pot').heat==='Ready',{},{timeout:18000});if(!burn){await press('e');check((await state()).held==='Woodland soup','Cooked soup collected on the universal plate')}else{await use(assembly);await use('extinguisher');await go('pot');await page.waitForFunction(()=>window.baraState.stations.find(s=>s.id==='pot').heat==='Warning',{},{timeout:16000});await shot('fire-and-rescue',async()=>{await page.waitForFunction(()=>window.baraState.stations.find(s=>s.id==='pot').heat==='Burning',{},{timeout:14000});await sleep(1000);await press('Space');await page.waitForFunction(()=>window.baraState.stations.find(s=>s.id==='pot').heat==='Extinguished',{},{timeout:14000});await sleep(900)});check((await state()).stations.find(s=>s.id==='pot').heat==='Extinguished','Warning progresses to fire and extinguisher puts it out');await press('e');check((await state()).stations.find(s=>s.id==='pot').heat==='Empty','Burnt batch cleared');await use('extinguisher');await use(assembly);await use('dishes')}}
async function serve(film=false){const before=(await state()).served;const action=async()=>{await go('serve');await press('e');for(let i=0;i<20&&(await state()).held;i++){await sleep(1000);await press('e')}await sleep(800)};if(film)await shot('delivery',action);else await action();check((await state()).served===before+1,'Correct dish served and scored');if(before===0)check(!(await state()).training,'First delivery starts round clock');snapshots['delivery-'+(before+1)]=await state();await screenshot('served-'+(before+1))}
async function wash(film=false){await page.waitForFunction(()=>window.baraState.dirty>0,{},{timeout:12000});await use('return');await use('sink');const action=async()=>{await sleep(250);await press('Space');await page.waitForFunction(()=>window.baraState.held==='Clean dish',{},{timeout:12000});await sleep(500)};if(film)await shot('washing',action);else await action();check((await state()).held==='Clean dish','One-tap washing returns a reusable clean dish');await use('dishes')}
async function dash(){const origin=level===2?{x:4,y:1}:level===3?{x:4,y:2}:{x:7,y:1};await goCell(origin);await face(world(origin.x+1,origin.y));const before=await state();await shot('dashing',async()=>{await sleep(300);await page.keyboard.down('d');await page.keyboard.press('Shift');await sleep(230);await page.keyboard.up('d');await sleep(800)});check(Math.hypot((await state()).x-before.x,(await state()).z-before.z)>.9,'Unlocked dash moves quickly through open floor')}
// Trailer-only recapture through the same public controls; no scene or simulation mutation.
async function orbit(yaw){
 const before=await state();await page.mouse.move(960,510);await page.mouse.down();await sleep(80);
 await page.mouse.move(960+(before.cameraTargetYaw-yaw)/180*1920,510,{steps:20});await sleep(100);await page.mouse.up();await sleep(750);
 const after=await state();check(Math.abs(after.cameraYaw-yaw)<.8,'Capture camera reaches '+yaw+' degree azimuth');
 check(Math.hypot(after.x-before.x,after.z-before.z)<.02,'Camera adjustment preserves chef position');
}
async function loadSoupForRetake(){
 await use('dishes');await use(assembly);await chop('carrot');await use(assembly);await chop('mushroom');await use(assembly);
 await use(assembly);await use('pot');check((await state()).stations.find(s=>s.id==='pot').heat==='Cooking','Retake uses a genuinely cooking soup batch');
}
async function recapturePotAngles(){
 check(level===2,'Pot retakes use the island kitchen');
 await loadSoupForRetake();await orbit(45);await screenshot('cooking-angle');
 snapshots.cooking=await state();await shot('cooking-diagonal',async()=>{await sleep(4700)});
 await page.waitForFunction(()=>window.baraState.stations.find(s=>s.id==='pot').heat==='Ready',{},{timeout:18000});await press('e');
 check((await state()).held==='Woodland soup','Diagonal soup shot ends in a collectible dish');
 await orbit(0);await serve();await wash();
 await loadSoupForRetake();await use(assembly);await use('extinguisher');await go('pot');await orbit(45);
 await page.waitForFunction(()=>window.baraState.stations.find(s=>s.id==='pot').heat==='Warning',{},{timeout:20000});
 await sleep(2700);await screenshot('warning-angle');snapshots.warning=await state();
 await shot('fire-and-rescue-diagonal',async()=>{
  const start=Date.now();await page.waitForFunction(()=>window.baraState.stations.find(s=>s.id==='pot').heat==='Burning',{},{timeout:12000});
  snapshots.ignition={seconds:(Date.now()-start)/1000,state:await state()};await screenshot('fire-angle');await sleep(1200);
  await press('Space');await sleep(450);await screenshot('spray-angle');snapshots.spray={seconds:(Date.now()-start)/1000,state:await state()};
  await page.waitForFunction(()=>window.baraState.stations.find(s=>s.id==='pot').heat==='Extinguished',{},{timeout:12000});
  snapshots.extinguished={seconds:(Date.now()-start)/1000,state:await state()};await screenshot('extinguished-angle');await sleep(1200);
 });
 check((await state()).stations.find(s=>s.id==='pot').heat==='Extinguished','Diagonal fire shot includes completed suppression');
 check(errors.length===0,'No browser errors during retakes');
 fs.writeFileSync(path.join(qa,'level-2.json'),JSON.stringify({passed:true,level,checks,snapshots,clips,errors},null,2));
 console.log('BARA_POT_RETAKE_OK');
}
(async()=>{try{
 browser=await chromium.launch({...(process.env.BARA_CHROME?{executablePath:process.env.BARA_CHROME}:{}),headless:true,args:['--enable-unsafe-swiftshader']});page=await browser.newPage({viewport:{width:1920,height:1080}});page.on('pageerror',e=>errors.push(e.message));await page.goto('http://127.0.0.1:54114/production/gameplay-round-01/?v=camera-orbit-01');await page.waitForFunction(()=>window.baraState?.phase==='Menu',{},{timeout:90000});
 await click('Choose your chef');await click('',[1,2,4,0][level-1]);await click('Done');await click('Choose kitchen');await click('Enter kitchen',level-1);await screenshot('guide');await click('Open the cafe');await sleep(900);
 // Precisely set a front-facing capture angle via the same mouse controls available to the player.
 let s=await state();await page.mouse.move(960,510);await page.mouse.down();await sleep(100);await page.mouse.move(960+s.cameraTargetYaw/180*1920,510,{steps:16});await sleep(200);await page.mouse.up();await sleep(1100);s=await state();await page.keyboard.down('[');await sleep((s.cameraTargetTilt-43)/28*1000);await page.keyboard.up('[');await sleep(1000);
 blocked=new Set((await state()).stations.map(s=>key(cell(s))));for(const b of layout.barriers||[])for(const [x,y] of b.cells||[])blocked.add(x+','+y);
 if(potRetake){await recapturePotAngles();return;}
 check((await state()).level===level,'Correct kitchen loaded');await shot('welcome',async()=>{await sleep(5000)});await screenshot('service');
 if(level===2)await soup(true);else await salad(true);await serve(true);await shot('customers',async()=>{await sleep(4000)});await wash(level===1||level===3);
 if(level>=2)await dash();
 if(level===1)await salad();else await soup();await serve();await wash();
 if(level>=3){await salad();await serve();await wash()}
 if(level===2)await soup(false,true);
 snapshots.beforeFinish=await state();await page.keyboard.press('Escape');await sleep(350);let paused=await state();await sleep(900);check(Math.abs((await state()).remaining-paused.remaining)<.1,'Pause freezes round countdown');await page.keyboard.press('Escape');await sleep(300);
 // Let genuine remaining service time expire; no clock or score mutation.
 while((await state()).phase==='Service'){const s=await state();console.log('L'+level+' ROUND '+Math.ceil(s.remaining)+'s served='+s.served+' missed='+s.missed);await sleep(Math.min(20000,Math.max(500,s.remaining*1000+1000)))}
 const result=await state();check(result.phase==='Results','Natural countdown reaches results');check(result.served>=2&&result.score>0,'Round records successful deliveries and score');check(result.missed>0,'Unfulfilled orders expire during service');await screenshot('results');snapshots.result=result;
 await click('Play again');check((await state()).phase==='Briefing'&&(await state()).score===0,'Retry cleanly resets the kitchen');check(errors.length===0,'No browser runtime errors');
 fs.writeFileSync(path.join(qa,'level-'+level+'.json'),JSON.stringify({passed:true,level,checks,snapshots,clips,errors},null,2));console.log('BARA_FULL_LEVEL_OK '+level);
 }catch(e){console.error(e);if(page){await screenshot('failure').catch(()=>{});snapshots.failure=await state().catch(()=>null)}fs.writeFileSync(path.join(qa,'level-'+level+'.json'),JSON.stringify({passed:false,level,checks,snapshots,clips,errors,error:String(e)},null,2));process.exitCode=1}
 finally{if(browser)await browser.close()}
})();
