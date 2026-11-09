const fs=require('node:fs'),assert=require('node:assert/strict'),WebSocket=require('D:/Đồ án cuối kì/frontend/node_modules/ws');
(async()=>{
 const tabs=await(await fetch('http://localhost:9248/json')).json();const ws=new WebSocket(tabs.find(t=>t.type==='page').webSocketDebuggerUrl);await new Promise((ok,no)=>{ws.once('open',ok);ws.once('error',no)});
 let seq=0;const requests=new Map();let callback=null;
 ws.on('message',data=>{const m=JSON.parse(data);if(m.id){const p=requests.get(m.id);requests.delete(m.id);m.error?p.no(m.error):p.ok(m.result)}
  if(m.method==='Network.requestWillBeSent'){const url=new URL(m.params.request.url);if(url.hostname==='localhost'&&url.pathname==='/payment/vnpay/return'){callback={responseCode:url.searchParams.get('vnp_ResponseCode'),transactionStatus:url.searchParams.get('vnp_TransactionStatus'),reference:url.searchParams.get('vnp_TxnRef'),providerTransactionNo:url.searchParams.get('vnp_TransactionNo')};fs.writeFileSync(__dirname+'/actual-callback-result.json',JSON.stringify(callback));}}
 });
 const send=(method,params={})=>new Promise((ok,no)=>{const id=++seq;requests.set(id,{ok,no});ws.send(JSON.stringify({id,method,params}))});
 const ev=async expression=>{const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value};
 const pause=ms=>new Promise(r=>setTimeout(r,ms));const wait=async(expression,label,attempts=180)=>{for(let i=0;i<attempts;i++){if(await ev(expression)){await pause(250);return}await pause(250)}throw Error('Timeout '+label)};
 const shot=async name=>{fs.writeFileSync(__dirname+'/'+name+'.png',Buffer.from((await send('Page.captureScreenshot',{format:'png'})).data,'base64'))};
 await send('Network.enable');
 assert.equal(await ev('location.hostname'),'sandbox.vnpayment.vn');
 await ev('document.getElementById("NCB").click()');await wait('!!document.getElementById("card_number_mask")','NCB form');
 await ev(fs.readFileSync(__dirname+'/fill-ncb.js','utf8'));await ev(fs.readFileSync(__dirname+'/continue-ncb.js','utf8'));
 await wait('!!document.getElementById("otpvalue") || !!document.getElementById("btnAgree") && document.getElementById("btnAgree").getClientRects().length>0','terms or OTP');
 if(!await ev('!!document.getElementById("otpvalue")'))await ev(fs.readFileSync(__dirname+'/agree-ncb.js','utf8'));
 await wait('!!document.getElementById("otpvalue")','Sandbox OTP');await ev(fs.readFileSync(__dirname+'/pay-ncb.js','utf8'));
 await wait('location.hostname==="localhost" && location.pathname.startsWith("/booking/complete/") && document.readyState==="complete"','return to receipt');
 assert.equal(callback?.responseCode,'00','VNPAY actual response success');assert.equal(callback?.transactionStatus,'00','VNPAY actual transaction status success');
 console.log('PASS actual VNPAY Sandbox returned success codes 00/00');
 await wait('document.querySelector("[data-booking-status]")?.dataset.bookingStatus==="PAID"','automatic settlement through registered IPN',400);
 assert.equal(await ev('!!document.getElementById("pay-vnpay")'),false,'paid order cannot pay again');
 await shot('actual-vnpay-paid');fs.writeFileSync(__dirname+'/actual-paid-result.json',JSON.stringify({...callback,receipt:await ev('location.pathname'),status:'PAID'}));
 console.log('PASS booking automatically became PAID after real Sandbox checkout; payment button removed');ws.close();
})().catch(e=>{console.error(e);process.exit(1)});
