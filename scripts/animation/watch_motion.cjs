const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const deps=require('../browser-deps.cjs');
const {chromium}=deps('playwright');
const timesByAction={ChefIdle:[0,.8,1.6,2.4],Walk:[0,.2,.4,.6],Dash:[0,.18,.35,.52],CarryIdle:[0,.8,1.6,2.4],CarryWalk:[0,.225,.45,.675],Throw:[0,.25,.46,.9],Chop:[0,.18,.36,.54],Wash:[.3,.9,1.5,2.9],CustomerIdle:[0,.8,1.6,2.4],CustomerEat:[0,.65,1.3,1.95],Extinguish:[0,.9,1.8,3.8],Overcook:[1.1,3.5,8,11.3]};
(async()=>{
  const browser=await chromium.launch({...(process.env.BARA_CHROME?{executablePath:process.env.BARA_CHROME}:{}),headless:true});
  try{
    const page=await browser.newPage({viewport:{width:760,height:760}});
    const tag=process.argv[2]||'gentle-motion-03';
    const folder=path.resolve('.local/motion-watch',tag);fs.mkdirSync(folder,{recursive:true});
    const clips=JSON.parse(fs.readFileSync('art/production/animations-round-01/clips.json')).clips;
    const filters=process.argv.slice(3);
    const selected=clips.filter(c=>!filters.length||filters.includes(c.id)||filters.includes(c.action));
    await page.goto('http://127.0.0.1:54114/production/animations-round-01/');
    await page.waitForFunction(()=>window.previewReady);
    for(const clip of selected){
      const times=timesByAction[clip.action];
      await page.evaluate(id=>choose(id),clip.id);await page.waitForFunction(()=>window.previewReady);
      await page.evaluate(async()=>{let v=document.querySelector('video');v.muted=true;v.controls=false;v.loop=false;v.currentTime=0;await v.play()});
      await page.waitForFunction(()=>document.querySelector('video').currentTime>=document.querySelector('video').duration-.1,{},{timeout:30000});
      await page.evaluate(()=>document.querySelector('video').pause());
      for(let i=0;i<times.length;i++){
        await page.evaluate(t=>new Promise(resolve=>{let v=document.querySelector('video');v.addEventListener('seeked',resolve,{once:true});v.currentTime=t+.001}),times[i]);
        await page.waitForFunction(t=>Math.abs(document.querySelector('video').currentTime-t)<.08,times[i]);
        await page.locator('video').screenshot({path:path.join(folder,clip.id+'-'+i+'.png')});
      }
    }
    fs.writeFileSync(path.join(folder,'reviewed.json'),JSON.stringify({source:'Decoded MP4 videos, played in Chrome and sampled at four phases',clips:selected.map(c=>({id:c.id,animal:c.animal,action:c.action,times:timesByAction[c.action]}))},null,2));
    console.log(JSON.stringify({folder,clips:selected.length}));
  }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
