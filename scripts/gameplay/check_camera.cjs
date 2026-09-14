const fs = require('fs'), path = require('path'), {createRequire} = require('module');
const deps = require('../browser-deps.cjs');
const {chromium} = deps('playwright');
(async () => {
  const browser = await chromium.launch({...(process.env.BARA_CHROME?{executablePath:process.env.BARA_CHROME}:{}),headless:true,args:['--enable-unsafe-swiftshader']});
  try {
    const page = await browser.newPage({viewport:{width:1280,height:720}}), errors=[], checks=[], views=[];
    page.on('pageerror',e=>errors.push(e.message));
    const out=path.resolve('art/production/gameplay-round-01/qa');
    const url='http://127.0.0.1:54114/production/gameplay-round-01/?v=camera-controls-01';
    const state=()=>page.evaluate(()=>window.baraState);
    const waitMenu=()=>page.waitForFunction(()=>window.baraState?.phase==='Menu',{}, {timeout:90000});
    const click=async(label,n=0)=>{const b=(await state()).buttons.filter(b=>b.label===label)[n];if(!b)throw Error('Missing '+label);const c=await page.locator('canvas').boundingBox();await page.mouse.click(c.x+(b.x+b.w/2)*c.width,c.y+(b.y+b.h/2)*c.height);await page.waitForTimeout(600);};
    const capture=async name=>{const s=await state();await page.screenshot({path:path.join(out,name+'.png')});views.push({name,state:s});};
    const require=(yes,label)=>{if(!yes)throw Error(label);checks.push(label);console.log('PASS',label);};
    const settle=async()=>{await page.waitForTimeout(1600);await page.waitForFunction(()=>Math.abs(window.baraState.cameraTilt-window.baraState.cameraTargetTilt)<.05,{}, {timeout:10000});};
    const key=async(k,ms)=>{await page.keyboard.down(k);await page.waitForTimeout(ms);await page.keyboard.up(k);await settle();};
    const frame=async label=>{const s=await state(),b=s.roomBounds;const x=b.x??b.m_XMin, y=b.y??b.m_YMin, w=b.width??b.m_Width,h=b.height??b.m_Height;require(x>=0&&y>=0&&x+w<=1&&y+h<=1,label);};
    await page.goto(url);await waitMenu();await click('Begin service');await click('Open the cafe');await page.waitForTimeout(1000);
    require(Math.abs((await state()).cameraTilt-60)<.1,'New saves start at a more overhead 60 degree view');await capture('camera-default');
    const before=await state();await page.mouse.move(620,350);await page.mouse.wheel(0,-120);await settle();const up=await state();
    require(up.cameraTilt>before.cameraTilt+1,'Mouse wheel up tilts toward overhead');require(Math.hypot(up.x-before.x,up.z-before.z)<.02,'Camera wheel does not move the chef');
    await page.mouse.wheel(0,120);await settle();require((await state()).cameraTilt<up.cameraTilt-1,'Mouse wheel down tilts toward the angled view');
    const mid=await state();await page.mouse.move(620,20);await page.mouse.wheel(0,-120);await settle();require(Math.abs((await state()).cameraTilt-mid.cameraTilt)<.1,'Scrolling the HUD does not change the camera');
    await key('[',2200);require(Math.abs((await state()).cameraTilt-35)<.1,'Keyboard tilts to lower limit without going below it');await frame('Whole room stays framed at low angle');await capture('camera-angled');
    await key(']',2300);require(Math.abs((await state()).cameraTilt-80)<.1,'Keyboard tilts to overhead limit without flipping');await frame('Whole room stays framed near overhead');await capture('camera-overhead');
    const movement=await state();await page.keyboard.down('w');await page.waitForTimeout(160);await page.keyboard.up('w');await page.waitForTimeout(250);const moved=await state();require(Math.hypot(moved.x-movement.x,moved.z-movement.z)>.1,'Chef still moves in the overhead view');
    await page.keyboard.press('h');await page.waitForTimeout(500);await capture('camera-guide');const paused=await state();await page.mouse.move(600,350);await page.mouse.wheel(0,240);await page.waitForTimeout(500);require((await state()).phase==='Paused'&&Math.abs((await state()).cameraTargetTilt-paused.cameraTargetTilt)<.1,'Camera does not react behind the tutorial');await click('Back to service');
    require(Math.abs((await state()).cameraTilt-80)<.1,'Closing help preserves the chosen view');
    await page.keyboard.press('c');await settle();require(Math.abs((await state()).cameraTilt-60)<.1,'C resets to the more overhead default');
    await key('[',350);let preferred=(await state()).cameraTargetTilt;require(preferred>35&&preferred<60,'An intermediate tilt is selectable');
    await page.reload();await waitMenu();require(Math.abs((await state()).cameraTilt-preferred)<.1,'Chosen camera angle survives page reload');await click('Choose kitchen');
    for(let level=1;level<=4;level++){
      await click('Enter kitchen',level-1);await capture('camera-level-'+level+'-guide');await click('Open the cafe');require(Math.abs((await state()).cameraTilt-preferred)<.1,'Kitchen '+level+' retains the preferred view');
      for(const [k,angle,name] of [['[',35,'angled'],[']',80,'overhead']]){await key(k,2400);await frame('Kitchen '+level+' fits at '+angle+' degrees');await capture('camera-level-'+level+'-'+name);}
      await key('[',(80-preferred)/28*1000); // Restore a custom view through real input before switching kitchens.
      // The exact selected value depends on frame timing, so carry the live preference forward.
      const next=(await state()).cameraTargetTilt;preferred=next;
      if(level<4){await page.keyboard.press('Escape');await page.waitForTimeout(350);await click('Main menu');await click('Choose kitchen');}
      // All following levels should preserve the current preference, not the earlier pulse's rounded value.
      views.push({name:'preference-before-next-'+level,angle:next});
    }
    require(errors.length===0,'No browser runtime errors');
    fs.writeFileSync(path.join(out,'camera-playtest.json'),JSON.stringify({passed:true,checks,views,errors},null,2));
    console.log('BARA_CAMERA_BROWSER_OK');
  } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
