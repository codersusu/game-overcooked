const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const deps=require('./browser-deps.cjs');const {chromium}=deps('playwright');
const out=path.resolve(__dirname,'../art/production/round-01');const data=JSON.parse(fs.readFileSync(path.join(out,'review-data.json')));fs.mkdirSync(path.join(out,'qa'),{recursive:true});
(async()=>{const browser=await chromium.launch({...(process.env.BARA_CHROME?{executablePath:process.env.BARA_CHROME}:{}),headless:true,args:['--use-angle=swiftshader','--enable-webgl']});const errors=[],checks=[];
try{const page=await browser.newPage({viewport:{width:1440,height:1120},deviceScaleFactor:1});page.on('pageerror',e=>errors.push(e.message));page.on('response',r=>{if(r.status()>=400)errors.push(r.status()+' '+r.url())});
await page.goto('http://127.0.0.1:54114/production/round-01/',{waitUntil:'networkidle'});
async function ready(label){await page.waitForFunction(()=>window.previewReady||window.previewError,{},{timeout:60000});const result=await page.evaluate(()=>{const v=document.getElementById('viewer');return {error:window.previewError,animations:v.availableAnimations,duration:v.duration,dimensions:v.getDimensions()}});if(result.error)throw Error(label+': '+result.error);checks.push({label,...result});}
await ready('room-1');
for(const room of data.rooms){if(room.id!=='1'){await page.selectOption('#assetSelect',room.id);await ready('room-'+room.id)}await page.locator('.stage').screenshot({path:path.join(out,'qa','room-'+room.id+'.png')});
for(const button of ['#viewStill','#viewBefore']){await page.click(button);await page.waitForFunction(()=>{const i=document.getElementById('still');return i.complete&&i.naturalWidth===1600});const src=await page.locator('#still').getAttribute('src');if(src.includes('archive/')!==(button==='#viewBefore'))throw Error('Incorrect comparison image');checks.push({label:'room-'+room.id+button,src});}await page.click('#view3d');}
if(!process.argv.includes('--rooms-only')&&!process.argv.includes('--environment-only')) {
await page.click('[data-tab="characters"]');await ready('initial-character');
for(const ch of data.characters){if(ch.id!==data.characters[0].id){await page.selectOption('#assetSelect',ch.id);await ready(ch.id)}if(ch.id.endsWith('male-chef'))await page.locator('.stage').screenshot({path:path.join(out,'qa',ch.id+'.png')});for(const motion of ch.motions||[]){await page.selectOption('#motion',motion.url);await ready(ch.id+' '+motion.label);const last=checks.at(-1);if(!last.animations.length||!last.duration)errors.push(ch.id+': missing playable clip');}}
await page.click('[data-tab="props"]');await ready('initial-prop');
for(const [i,p]of data.props.entries()){if(i){await page.selectOption('#assetSelect',p.id);await ready(p.id)}if(['counter','dish-salad','tomato-whole'].includes(p.id))await page.locator('.stage').screenshot({path:path.join(out,'qa',p.id+'.png')});}
}
if(process.argv.includes('--environment-only')){await page.click('[data-tab="props"]');await ready('initial-prop');await page.selectOption('#subset','Decor');await ready('initial-decor');for(const p of data.props.filter(p=>p.category==='Decor')){await page.selectOption('#assetSelect',p.id);await ready(p.id);await page.locator('.stage').screenshot({path:path.join(out,'qa',p.id+'.png')});}}
await page.click('[data-tab="rooms"]');await ready('return-to-rooms');await page.screenshot({path:path.join(out,'qa','review-desktop.png'),fullPage:true});
await page.setViewportSize({width:640,height:920});await page.screenshot({path:path.join(out,'qa','review-narrow.png'),fullPage:true});
fs.writeFileSync(path.join(out,process.argv.includes('--environment-only')?'viewer-environment-check.json':process.argv.includes('--rooms-only')?'viewer-rooms-final-check.json':'viewer-check.json'),JSON.stringify({checks,errors},null,2));console.log(JSON.stringify({loaded:checks.length,errors}));if(errors.length)process.exitCode=1;
}finally{await browser.close()}})().catch(e=>{console.error(e);process.exitCode=1});
