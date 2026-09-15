const fs=require('fs'),{chromium}=require('../browser-deps.cjs')('playwright');
(async()=>{
 const b=await chromium.launch({executablePath:process.env.BARA_CHROME,headless:true});const p=await b.newPage({viewport:{width:1600,height:1120}}),errors=[];p.on('pageerror',e=>errors.push(e.message));
 try{
  await p.goto('http://127.0.0.1:54114/trailers/round-02/');await p.waitForFunction(()=>document.querySelector('video').readyState>=2,null,{timeout:30000});
  const meta=await p.evaluate(async()=>{const v=document.querySelector('video');v.muted=true;await v.play();return {duration:v.duration,width:v.videoWidth,height:v.videoHeight}});
  const expected=JSON.parse(fs.readFileSync('art/trailers/round-02/edit.json')).duration;
  await p.waitForTimeout(13000);await p.screenshot({path:'art/trailers/round-02/qa/playback-chopping.png'});await p.waitForTimeout(21000);await p.screenshot({path:'art/trailers/round-02/qa/playback-reminder.png'});
  await p.waitForFunction(()=>document.querySelector('video').ended,null,{timeout:35000});
  meta.ended=true;meta.frames=await p.evaluate(()=>{const q=document.querySelector('video').getVideoPlaybackQuality();return {total:q.totalVideoFrames,dropped:q.droppedVideoFrames}});meta.errors=errors;meta.passed=!errors.length&&Math.abs(meta.duration-expected)<.15&&meta.duration<=60&&meta.width===1920&&meta.height===1080;
  fs.writeFileSync('art/trailers/round-02/qa/playback-validation.json',JSON.stringify(meta,null,2)+'\n');if(!meta.passed)throw Error(JSON.stringify(meta));console.log('VOICE_TRAILER_PLAYBACK_OK',JSON.stringify(meta));
 }finally{await b.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
