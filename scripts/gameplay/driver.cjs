// Keyboard-only navigation shared by gameplay media captures. Never mutates game state.
const fs=require('fs');
module.exports=function(page,level){
 const layout=JSON.parse(fs.readFileSync('art/concepts/levels-round-02/levels.json'))[level-1],unit=.92;
 let blocked;const state=()=>page.evaluate(()=>window.baraState),sleep=ms=>page.waitForTimeout(ms),key=p=>p.x+','+p.y;
 const world=(x,y)=>({x:((layout.cols-1)/2-x)*unit,z:y*unit});const cell=s=>({x:Math.round((layout.cols-1)/2-s.x/unit),y:Math.round(s.z/unit)});
 const free=p=>p.x>=0&&p.x<layout.cols&&p.y>=0&&p.y<layout.rows&&!blocked.has(key(p));
 function route(a,b){let queue=[a],prev=new Map([[key(a),null]]);for(let n=0;n<queue.length;n++){let p=queue[n];if(key(p)===key(b)){let r=[];while(p){r.push(p);p=prev.get(key(p))}r.reverse();return r.filter((p,i)=>i===0||i===r.length-1||(r[i+1].x-r[i].x!==r[i].x-r[i-1].x||r[i+1].y-r[i].y!==r[i].y-r[i-1].y))}for(const [x,y] of [[1,0],[-1,0],[0,1],[0,-1]]){const q={x:p.x+x,y:p.y+y};if(free(q)&&!prev.has(key(q))){prev.set(key(q),p);queue.push(q)}}}return null}
 function direction(s,dx,dz){const r=dx*s.rightX+dz*s.rightZ,f=dx*s.forwardX+dz*s.forwardZ,m=Math.max(Math.abs(r),Math.abs(f)),keys=[];if(Math.abs(r)>m*.42)keys.push(r>0?'d':'a');if(Math.abs(f)>m*.42)keys.push(f>0?'w':'s');return keys}
 async function push(keys,ms){for(const k of keys)await page.keyboard.down(k);await sleep(ms);for(const k of keys)await page.keyboard.up(k);await sleep(175)}
 async function travel(p){for(let i=0;i<25;i++){const s=await state();if(s.phase!=='Service')throw Error('Service ended');const dx=p.x-s.x,dz=p.z-s.z,d=Math.hypot(dx,dz);if(d<.13)return;await push(direction(s,dx,dz),Math.max(25,Math.min(650,d/3.15*970)))}throw Error('Could not reach waypoint')}
 async function face(p){const s=await state();await push(direction(s,p.x-s.x,p.z-s.z),42)}
 async function go(id){const s=await state(),t=s.stations.find(s=>s.id===id);if(!t)throw Error('Missing station '+id);const p=cell(t);const options=[[1,0],[-1,0],[0,1],[0,-1]].map(([x,y])=>({x:p.x+x,y:p.y+y})).filter(free).map(p=>route(cell(s),p)).filter(Boolean);options.sort((a,b)=>length(a)-length(b));if(!options.length)throw Error('No route '+id);for(const c of options[0])await travel(world(c.x,c.y));await face(t);await sleep(150);if((await state()).focused!==id)throw Error('Could not focus '+id)}
 function length(r){return r.reduce((v,p,i)=>v+(i?Math.abs(p.x-r[i-1].x)+Math.abs(p.y-r[i-1].y):0),0)}
 async function press(k){await page.keyboard.press(k);await sleep(250)}
 async function use(id){await go(id);await press('e')}
 async function click(label,index=0){const b=(await state()).buttons.filter(b=>b.label===label)[index];if(!b)throw Error('Missing button '+label);const c=await page.locator('canvas').boundingBox();await page.mouse.click(c.x+(b.x+b.w/2)*c.width,c.y+(b.y+b.h/2)*c.height);await sleep(550)}
 async function orbit(yaw){const s=await state();await page.mouse.move(960,510);await page.mouse.down();await sleep(80);await page.mouse.move(960+(s.cameraTargetYaw-yaw)/180*1920,510,{steps:20});await sleep(100);await page.mouse.up();await sleep(900)}
 async function boot(){await page.waitForFunction(()=>window.baraState?.phase==='Menu',null,{timeout:120000});await click('Choose your chef');await click('',0);await click('Done');await click('Choose kitchen');await click('Enter kitchen',level-1);await click('Open the cafe');await sleep(900);await orbit(30);const s=await state();if(s.cameraTargetTilt>47){await page.keyboard.down('[');await sleep((s.cameraTargetTilt-47)/28*1000);await page.keyboard.up('[');await sleep(800)}blocked=new Set((await state()).stations.filter(s=>s.kind!=='extinguisher').map(s=>key(cell(s))));for(const b of layout.barriers||[])for(const [x,y] of b.cells||[])blocked.add(x+','+y)}
 const assembly=level===1?'C1':'C2';
 async function chopHeld(){await use('prep');await press('Space');await page.waitForFunction(()=>window.baraState.stations.find(s=>s.id==='prep').item.startsWith('Chopped'),null,{timeout:12000});await press('e')}
 async function chop(id){await use(id);await chopHeld()}
 async function loadSoup(){await use('dishes');await use(assembly);await chop('carrot');await use(assembly);await chop('mushroom');await use(assembly);await use(assembly);await use('pot');if((await state()).stations.find(s=>s.id==='pot').heat!=='Cooking')throw Error('Soup not cooking')}
 async function serve(){const n=(await state()).served;await use('serve');if((await state()).served!==n+1)throw Error('Dish not served')}
 return {state,sleep,press,use,go,click,orbit,boot,assembly,chopHeld,chop,loadSoup,serve};
};
