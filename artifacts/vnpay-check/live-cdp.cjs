const fs=require('node:fs'),WebSocket=require('D:/Đồ án cuối kì/frontend/node_modules/ws');
(async()=>{
 const tabs=await(await fetch('http://localhost:9248/json')).json();const ws=new WebSocket(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);await new Promise((ok,no)=>{ws.once('open',ok);ws.once('error',no)});
 let seq=0;const requests=new Map();ws.on('message',data=>{const m=JSON.parse(data);if(m.id){const p=requests.get(m.id);requests.delete(m.id);m.error?p.no(m.error):p.ok(m.result)}});
 const send=(method,params={})=>new Promise((ok,no)=>{const id=++seq;requests.set(id,{ok,no});ws.send(JSON.stringify({id,method,params}))});
 const ev=async expression=>{const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value};
 if(process.argv[2]==='--close'){await send('Browser.close');ws.close();return;}
 if(process.argv[2]) { const expression=process.argv[2].startsWith('@')?fs.readFileSync(__dirname+'/'+process.argv[2].slice(1),'utf8'):process.argv[2];console.log(JSON.stringify(await ev(expression))); }
 await new Promise(r=>setTimeout(r,1800));
 if(process.argv[3])fs.writeFileSync(__dirname+'/'+process.argv[3]+'.png',Buffer.from((await send('Page.captureScreenshot',{format:'png'})).data,'base64'));
 console.log(JSON.stringify(await ev('({host:location.hostname,path:location.pathname,text:document.body.innerText.slice(0,2500),inputs:[...document.querySelectorAll("input")].map(x=>({id:x.id,name:x.name,type:x.type,placeholder:x.placeholder})),buttons:[...document.querySelectorAll("button")].map(x=>({id:x.id,text:x.textContent.trim()}))})')));
 ws.close();
})().catch(e=>{console.error(e);process.exit(1)});
