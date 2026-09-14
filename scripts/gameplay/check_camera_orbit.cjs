const fs=require('fs'),path=require('path'),{createRequire}=require('module');
const deps=require('../browser-deps.cjs');
const {chromium}=deps('playwright');
(async()=>{
 const browser=await chromium.launch({...(process.env.BARA_CHROME?{executablePath:process.env.BARA_CHROME}:{}),headless:true,args:['--enable-unsafe-swiftshader']});
 try{
  const page=await browser.newPage({viewport:{width:1280,height:720}}),checks=[],views=[],errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  const out=path.resolve('art/production/gameplay-round-01/qa'),url='http://127.0.0.1:54114/production/gameplay-round-01/?v=camera-orbit-01';
  const state=()=>page.evaluate(()=>window.baraState);
  const waitMenu=()=>page.waitForFunction(()=>window.baraState?.phase==='Menu',{}, {timeout:90000});
  const check=(condition,label)=>{if(!condition)throw Error(label);checks.push(label);console.log('PASS',label)};
  const click=async(label,n=0)=>{const b=(await state()).buttons.filter(b=>b.label===label)[n];if(!b)throw Error('Missing '+label);const c=await page.locator('canvas').boundingBox();await page.mouse.click(c.x+(b.x+b.w/2)*c.width,c.y+(b.y+b.h/2)*c.height);await page.waitForTimeout(550)};
  const settle=async()=>{await page.waitForTimeout(1300);await page.waitForFunction(()=>Math.abs(window.baraState.cameraYaw-window.baraState.cameraTargetYaw)<.05&&Math.abs(window.baraState.cameraTilt-window.baraState.cameraTargetTilt)<.05,{}, {timeout:10000})};
  const capture=async name=>{const s=await state();await page.screenshot({path:path.join(out,name+'.png')});views.push({name,state:s})};
  const frame=async label=>{const b=(await state()).roomBounds,x=b.x??b.m_XMin,y=b.y??b.m_YMin,w=b.width??b.m_Width,h=b.height??b.m_Height;check(x>=0&&y>=0&&x+w<=1&&y+h<=1,label)};
  const drag=async(x1,x2,y=380,button='left')=>{await page.mouse.move(x1,y);await page.mouse.down({button});await page.waitForTimeout(100);await page.mouse.move(x2,y,{steps:20});await page.waitForTimeout(200);await page.mouse.up({button});await settle()};
  const key=async(k,ms)=>{await page.keyboard.down(k);await page.waitForTimeout(ms);await page.keyboard.up(k);await settle()};
  await page.goto(url);await waitMenu();await click('Begin service');await click('Open the cafe');await page.waitForTimeout(1000);
  const original=await state();await drag(510,830);const right=await state();
  check(right.cameraYaw<original.cameraYaw-20,'Dragging right rotates the horizontal view');check(Math.abs(right.cameraTilt-original.cameraTilt)<.1,'Horizontal drag preserves vertical tilt');check(Math.hypot(right.x-original.x,right.z-original.z)<.03,'Dragging does not move the chef');await frame('Kitchen remains framed after right drag');await capture('camera-drag-right');
  await drag(830,430);const left=await state();check(left.cameraYaw>right.cameraYaw+20,'Dragging left rotates back');await capture('camera-drag-left');
  await drag(440,620,380,'right');check((await state()).cameraYaw<left.cameraYaw-10,'Right-button dragging also rotates the camera');
  const gui=await state();await drag(400,650,35);check(Math.abs((await state()).cameraYaw-gui.cameraYaw)<.1,'Dragging the HUD does not rotate the scene');
  await key(',',850);const comma=await state();check(comma.cameraYaw>gui.cameraYaw+20,'Comma turns the view left');await key('.',550);check((await state()).cameraYaw<comma.cameraYaw-15,'Period turns the view right');
  const movement=await state();await page.keyboard.down('d');await page.waitForTimeout(140);await page.keyboard.up('d');await page.waitForTimeout(250);const moved=await state();check((moved.x-movement.x)*movement.rightX+(moved.z-movement.z)*movement.rightZ>.1,'D still moves toward screen right after rotation');
  const beforeTilt=await state();await page.mouse.move(650,380);await page.mouse.wheel(0,-120);await settle();check((await state()).cameraTilt>beforeTilt.cameraTilt+1&&Math.abs((await state()).cameraYaw-beforeTilt.cameraYaw)<.1,'Wheel still tilts without changing horizontal rotation');
  const beforeHelp=await state();await page.keyboard.press('h');await page.waitForTimeout(450);await drag(510,830);check((await state()).phase==='Paused'&&Math.abs((await state()).cameraTargetYaw-beforeHelp.cameraTargetYaw)<.1,'Picture guide blocks camera dragging');await click('Back to service');await settle();check(Math.abs((await state()).cameraYaw-beforeHelp.cameraYaw)<.1,'Closing guide preserves rotation');
  await page.keyboard.press('c');await settle();const reset=await state();check(Math.abs(reset.cameraYaw-original.cameraYaw)<.1&&Math.abs(reset.cameraTilt-60)<.1,'C resets both horizontal rotation and tilt');
  await drag(500,640);const saved=await state();await page.reload();await waitMenu();check(Math.abs((await state()).cameraYaw-saved.cameraYaw)<.1,'Horizontal preference survives page reload');await click('Choose kitchen');
  let preferred=saved.cameraYaw;
  for(let level=1;level<=4;level++){
   await click('Enter kitchen',level-1);await capture('orbit-level-'+level+'-guide');await click('Open the cafe');check(Math.abs((await state()).cameraYaw-preferred)<.1,'Kitchen '+level+' keeps horizontal preference');
   await key(',',3600);check(Math.abs((await state()).cameraYaw-80)<.1,'Kitchen '+level+' left limit works');await frame('Kitchen '+level+' fully visible from left');await capture('orbit-level-'+level+'-left');
   await key('.',3600);check(Math.abs((await state()).cameraYaw+80)<.1,'Kitchen '+level+' right limit works');await frame('Kitchen '+level+' fully visible from right');await capture('orbit-level-'+level+'-right');
   await page.keyboard.press('c');await settle();preferred=(await state()).cameraTargetYaw;
   if(level<4){await page.keyboard.press('Escape');await page.waitForTimeout(400);await click('Main menu');await click('Choose kitchen')}
  }
  check(errors.length===0,'No browser runtime errors');fs.writeFileSync(path.join(out,'camera-orbit-playtest.json'),JSON.stringify({passed:true,checks,views,errors},null,2));console.log('BARA_CAMERA_ORBIT_OK');
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
