const fs=require('node:fs');const assert=require('node:assert/strict');const WebSocket=require('D:/Đồ án cuối kì/frontend/node_modules/ws');
(async()=>{
 const tabs=await(await fetch('http://localhost:9242/json')).json();const socket=new WebSocket(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);await new Promise((resolve,reject)=>{socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
 let id=0;const pending=new Map();socket.addEventListener('message',e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);m.error?p.reject(m.error):p.resolve(m.result);}});
 const send=(method,params={})=>new Promise((resolve,reject)=>{const i=++id;pending.set(i,{resolve,reject});socket.send(JSON.stringify({id:i,method,params}));});
 const evaluate=async expression=>{const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw new Error(JSON.stringify(r.exceptionDetails));return r.result.value;};
 const pause=ms=>new Promise(r=>setTimeout(r,ms));
 const navigate=async path=>{await send('Page.navigate',{url:'http://localhost:5032'+path});await pause(500);for(let i=0;i<100;i++){if(await evaluate('document.readyState === "complete" && !!document.querySelector(".auth-form")')){await pause(500);return;}await pause(150);}throw new Error('Page not loaded '+path);};
 await send('Page.enable');await send('Emulation.setDeviceMetricsOverride',{width:1440,height:1050,deviceScaleFactor:1,mobile:false});
 for(const path of ['/login','/register','/admin/login']){
   await navigate(path);assert.equal(await evaluate('document.documentElement.scrollWidth <= innerWidth'),true,path+' desktop overflow');assert.equal(await evaluate('!!document.querySelector("input[name=__RequestVerificationToken]")'),true);
   await evaluate('document.getElementById("Password").value="UiTestPassword123";document.querySelector("[data-toggle-password]").click()');
   assert.equal(await evaluate('document.getElementById("Password").type'),'text');await evaluate('document.querySelector("[data-toggle-password]").click()');assert.equal(await evaluate('document.getElementById("Password").type'),'password');
   assert.equal(await evaluate('document.querySelector(".auth-form").checkValidity()'),false,'empty email must fail validation');
   fs.writeFileSync(__dirname+'/'+path.replaceAll('/','')+'-desktop.png',Buffer.from((await send('Page.captureScreenshot',{format:'png'})).data,'base64'));
 }
 await send('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});
 for(const path of ['/login','/register','/admin/login']){
   await navigate(path);assert.equal(await evaluate('document.documentElement.scrollWidth <= innerWidth'),true,path+' mobile overflow');
   fs.writeFileSync(__dirname+'/'+path.replaceAll('/','')+'-mobile.png',Buffer.from((await send('Page.captureScreenshot',{format:'png'})).data,'base64'));
 }
 console.log('PASS login/register/admin desktop and mobile, password toggle, antiforgery fields, required form validation');await send('Browser.close');
})().catch(e=>{console.error(e);process.exit(1);});
