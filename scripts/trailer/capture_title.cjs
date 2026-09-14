const fs=require('fs'),path=require('path');
const {chromium}=require('../browser-deps.cjs')('playwright');
(async()=>{const browser=await chromium.launch({...(process.env.BARA_CHROME?{executablePath:process.env.BARA_CHROME}:{}),headless:true});const p=await browser.newPage({viewport:{width:1920,height:1080}});await p.goto('file://'+path.resolve('art/trailers/round-01/title-card.html'));await p.evaluate(()=>document.fonts.ready);await p.screenshot({path:'art/trailers/round-01/title-card.png'});await browser.close()})();
