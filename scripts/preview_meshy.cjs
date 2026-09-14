const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const {createRequire} = require('node:module');
const deps = require('./browser-deps.cjs');
const {chromium} = deps('playwright');
const root = path.resolve(__dirname, '../art/experiments/meshy-capybara-01');
const types = {'.html':'text/html','.png':'image/png','.glb':'model/gltf-binary','.json':'application/json'};
const server = http.createServer((request,response)=>{
  const relative = decodeURIComponent(new URL(request.url,'http://localhost').pathname);
  const file = path.resolve(root, '.' + (relative==='/'?'/preview.html':relative));
  if(!file.startsWith(root+path.sep)){response.writeHead(403);response.end();return;}
  if(!fs.existsSync(file)||!fs.statSync(file).isFile()){response.writeHead(404);response.end();return;}
  response.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream','Cache-Control':'no-store'});
  fs.createReadStream(file).pipe(response);
});
async function main(){
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const url=`http://127.0.0.1:${server.address().port}/`;
  if(process.argv.includes('--serve')){console.log(url);return;}
  const browser=await chromium.launch({...(process.env.BARA_CHROME?{executablePath:process.env.BARA_CHROME}:{}),headless:true,args:['--use-angle=swiftshader','--enable-webgl']});
  try{
    const page=await browser.newPage({viewport:{width:1100,height:780},deviceScaleFactor:1});
    const errors=[];
    page.on('pageerror',e=>errors.push(e.message));
    await page.goto(url,{waitUntil:'networkidle',timeout:60000});
    await page.waitForFunction(()=>window.previewReady||window.previewError,{},{timeout:60000});
    if(await page.evaluate(()=>window.previewError))throw Error('Initial model failed to load');
    const screenshots=path.join(root,'screenshots');fs.mkdirSync(screenshots,{recursive:true});
    const modes=process.argv.includes('--all') ? [
      ['model','model/model.glb'],['rig','rig/rigged_character_glb.glb'],
      ['walk','rig/walking_glb.glb'],['run','rig/running_glb.glb'],['jump','animation/animation_glb.glb']
    ]:[['model','model/model.glb']];
    const results=[];
    for(const [name,file] of modes){
      if(!fs.existsSync(path.join(root,file)))continue;
      if(name!=='model'){
        await page.selectOption('#motion',file);
        await page.waitForFunction(()=>window.previewReady||window.previewError,{},{timeout:60000});
        if(await page.evaluate(()=>window.previewError))throw Error(name+' failed to load');
      }
      const data=await page.evaluate(()=>{const v=document.getElementById('viewer');v.pause();v.currentTime=v.duration?Math.min(.45,v.duration/3):0;return {animations:v.availableAnimations,duration:v.duration,dimensions:v.getDimensions()};});
      await page.waitForTimeout(350);
      await page.screenshot({path:path.join(screenshots,name+'.png'),fullPage:true});
      if(data.duration){
        for(let frame=0;frame<4;frame++){
          await page.evaluate(t=>{document.getElementById('viewer').currentTime=t;},data.duration*frame/4);
          await page.waitForTimeout(120);
          await page.locator('.stage').screenshot({path:path.join(screenshots,`${name}-frame-${frame}.png`)});
        }
        if(process.argv.includes('--record')){
          const directory=path.join(root,'motion-frames',name);fs.mkdirSync(directory,{recursive:true});
          const count=Math.max(2,Math.ceil(data.duration*24));
          for(let frame=0;frame<count;frame++){
            await page.evaluate(t=>{document.getElementById('viewer').currentTime=t;},data.duration*frame/count);
            await page.waitForTimeout(45);
            await page.locator('.stage').screenshot({path:path.join(directory,`${String(frame).padStart(4,'0')}.png`)});
          }
          data.recordedFrames=count;
        }
      }
      results.push({name,...data});
    }
    fs.writeFileSync(path.join(root,'viewer-check.json'),JSON.stringify({results,errors},null,2)+'\n');
    console.log(JSON.stringify({results,errors}));
    if(errors.length)process.exitCode=1;
  }finally{await browser.close();server.close();}
}
main().catch(e=>{console.error(e.message);server.close();process.exitCode=1;});
