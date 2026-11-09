const fs=require('node:fs'),assert=require('node:assert/strict'),WebSocket=require('D:/Đồ án cuối kì/frontend/node_modules/ws');
(async()=>{
 const f=JSON.parse(fs.readFileSync(__dirname+'/fixture.json','utf8'));const b=JSON.parse(fs.readFileSync(__dirname+'/browser-result.json','utf8'));
 const tabs=await(await fetch('http://localhost:9248/json')).json();const ws=new WebSocket(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);await new Promise((ok,no)=>{ws.once('open',ok);ws.once('error',no)});
 let seq=0;const requests=new Map();ws.on('message',data=>{const m=JSON.parse(data);if(m.id){const p=requests.get(m.id);requests.delete(m.id);m.error?p.no(m.error):p.ok(m.result)}});
 const send=(method,params={})=>new Promise((ok,no)=>{const id=++seq;requests.set(id,{ok,no});ws.send(JSON.stringify({id,method,params}))});
 const ev=async expression=>{const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value};
 const pause=ms=>new Promise(r=>setTimeout(r,ms));const wait=async expression=>{for(let i=0;i<150;i++){if(await ev(expression)){await pause(300);return}await pause(150)}throw Error('Timeout '+expression)};
 const go=async path=>{await send('Page.navigate',{url:'http://localhost:5037'+path});await pause(600);await wait('document.readyState==="complete"')};
 const shot=async name=>{await pause(600);fs.writeFileSync(__dirname+'/'+name+'.png',Buffer.from((await send('Page.captureScreenshot',{format:'png'})).data,'base64'))};
 await send('Page.enable');await send('Emulation.setDeviceMetricsOverride',{width:1440,height:1100,deviceScaleFactor:1,mobile:false});
 await go(b.receipt);assert.equal(await ev('document.querySelector("[data-booking-status]").dataset.bookingStatus'),'PAID');assert.equal(await ev('!!document.getElementById("pay-vnpay")'),false);await shot('payment-success-simulated');
 await go('/admin/login?returnUrl=%2Fadmin%2Fpayments');await ev('document.querySelector("[name=Email]").value='+JSON.stringify(f.emails[0])+';document.querySelector("[name=Password]").value='+JSON.stringify(f.password)+';document.querySelector("[name=Password]").form.requestSubmit()');await wait('location.pathname==="/admin/payments" && document.readyState==="complete"');
 assert.equal(await ev('document.querySelector(".nav-item.active").getAttribute("href")'),'/admin/payments');assert.equal(await ev('document.body.textContent.includes("Cần đối soát")'),true);assert.equal(await ev('document.documentElement.scrollWidth<=innerWidth'),true);await shot('admin-payments-desktop');
 await ev('document.querySelector("select[name=status]").value="REVIEW_REQUIRED";document.querySelector("select[name=status]").form.requestSubmit()');await wait('location.search.includes("REVIEW_REQUIRED") && document.readyState==="complete"');assert.equal(await ev('[...document.querySelectorAll("tbody tr")].every(x=>x.textContent.includes("Cần đối soát"))'),true);
 await send('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});await pause(300);assert.equal(await ev('document.documentElement.scrollWidth<=innerWidth'),true,'mobile admin width');await shot('admin-payments-mobile');
 console.log('PASS simulated paid receipt removes payment button, admin login/menu, review filtering and desktop/mobile payments layout');await send('Browser.close');
})().catch(e=>{console.error(e);process.exit(1)});
