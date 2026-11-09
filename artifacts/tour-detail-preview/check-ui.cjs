const fs = require('node:fs');
const assert = require('node:assert/strict');
const WebSocket = require('D:/Đồ án cuối kì/frontend/node_modules/ws');
(async () => {
  const base = 'http://localhost:5031';
  const paths = new Set();
  for(const page of [1,2,3]) {
    const html=await (await fetch(base+'/tours?page='+page)).text();
    for(const match of html.matchAll(/class="detailBtn" href="([^"]+)"/g)) paths.add(match[1].replaceAll('&amp;','&'));
  }
  let noSchedule=0;
  for(const path of paths) {
    const response=await fetch(base+path); const html=await response.text();
    assert.equal(response.status,200,path+' failed');
    assert.ok(html.includes('LỊCH KHỞI HÀNH') && html.includes('LỊCH TRÌNH'));
    if(html.includes('Chưa có lịch khởi hành sắp tới.')) noSchedule++;
  }
  assert.equal((await fetch(base+'/tour/THIS-TOUR-DOES-NOT-EXIST')).status,404);
  const invalid=await fetch(base+'/tour/CT-HL?departureId=2147483647');
  assert.equal(invalid.status,200); assert.ok((await invalid.text()).includes('Lịch khởi hành đã chọn không còn khả dụng'));
  console.log('PASS '+paths.size+' public detail pages, '+noSchedule+' without upcoming dates, invalid departure and 404');
  const targets=await (await fetch('http://localhost:9241/json')).json();
  const socket=new WebSocket(targets.find(t=>t.type==='page').webSocketDebuggerUrl);
  await new Promise((resolve,reject)=>{socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
  let id=0;const pending=new Map();
  socket.addEventListener('message',event=>{const message=JSON.parse(event.data);if(message.id){const h=pending.get(message.id);pending.delete(message.id);message.error?h.reject(message.error):h.resolve(message.result);}});
  const send=(method,params={})=>new Promise((resolve,reject)=>{const key=++id;pending.set(key,{resolve,reject});socket.send(JSON.stringify({id:key,method,params}));});
  const evaluate=async expression=>{const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw new Error(JSON.stringify(r.exceptionDetails));return r.result.value;};
  const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
  const navigate=async path=>{await send('Page.navigate',{url:base+path});await pause(400);for(let i=0;i<100;i++){if(await evaluate('document.readyState === "complete" && !!document.querySelector(".tourTitle")')){await pause(300);return;}await pause(150);}throw new Error('Page did not load');};
  await send('Page.enable');await send('Emulation.setDeviceMetricsOverride',{width:1440,height:1100,deviceScaleFactor:1,mobile:false});
  await navigate('/tour/CT-HL');
  assert.equal(await evaluate('document.documentElement.scrollWidth <= innerWidth'),true,'desktop overflow');
  assert.equal(await evaluate('document.getElementById("user-header").classList.contains("scrolled")'),true);
  fs.writeFileSync(__dirname+'/detail-desktop.png',Buffer.from((await send('Page.captureScreenshot',{format:'png'})).data,'base64'));
  const departure=await evaluate('document.querySelector("[data-select-departure]")?.dataset.selectDeparture');
  assert.ok(departure,'missing departure');
  await evaluate('document.querySelector("[data-select-departure]").click()');
  assert.equal(await evaluate('new URL(location.href).searchParams.get("departureId")'),departure);
  assert.equal(await evaluate('[...document.querySelectorAll("[data-sidebar-departure]")].find(p=>!p.hidden)?.dataset.sidebarDeparture'),departure);
  assert.equal(await evaluate('[...document.querySelectorAll("[data-departure-panel]")].find(p=>!p.hidden)?.dataset.departurePanel'),departure);
  assert.equal(await evaluate('[...document.querySelectorAll("[data-departure-policy]")].find(p=>!p.hidden)?.dataset.departurePolicy'),departure);
  await navigate('/tour/CT-HL?departureId='+departure);
  assert.equal(await evaluate('document.getElementById("departures").dataset.selectedDeparture'),departure);
  const thumbs=await evaluate('document.querySelectorAll("[data-gallery-index]").length');
  if(thumbs>1) {await evaluate('document.querySelectorAll("[data-gallery-index]")[1].click()');assert.equal(await evaluate('document.querySelectorAll("[data-gallery-index]")[1].getAttribute("aria-pressed")'),'true');}
  if(thumbs) {await evaluate('document.getElementById("gallery-open").click()');assert.equal(await evaluate('document.getElementById("tour-gallery-dialog").open'),true);await evaluate('document.getElementById("tour-gallery-dialog").close()');}
  await evaluate('document.querySelector(".td-itinerary details")?.setAttribute("open","")');
  await send('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});await evaluate('window.scrollTo(0,0)');await pause(800);
  assert.equal(await evaluate('document.documentElement.scrollWidth <= innerWidth'),true,'mobile overflow');
  fs.writeFileSync(__dirname+'/detail-mobile.png',Buffer.from((await send('Page.captureScreenshot',{format:'png'})).data,'base64'));
  await evaluate('document.getElementById("departures").scrollIntoView()');await pause(600);
  fs.writeFileSync(__dirname+'/detail-calendar-mobile.png',Buffer.from((await send('Page.captureScreenshot',{format:'png'})).data,'base64'));
  assert.equal(await evaluate('document.querySelector(".calendarGridArea").getBoundingClientRect().right <= innerWidth'),true,'calendar overflow');
  const homepage=await (await fetch(base)).text();assert.ok(homepage.includes('class="productCard" href="/tour/'));assert.ok(!homepage.includes('data-tour-dialog='));
  const detailScript=await (await fetch(base+'/js/tour-detail.js')).text();
  await send('Page.navigate',{url:'about:blank'});await pause(300);
  await evaluate('window.detailXss = 0; const test = document.createElement("div"); test.id="sanitize-check"; test.dataset.richText=""; test.textContent='+JSON.stringify('<p onclick="window.detailXss=1">Good <strong>text</strong></p><script>window.detailXss=2</script><a href="javascript:window.detailXss=3">bad</a><img src="x" onerror="window.detailXss=4"><svg onload="window.detailXss=5"></svg>')+'; document.body.append(test)');
  await evaluate(detailScript);await pause(100);
  assert.equal(await evaluate('window.detailXss'),0);assert.equal(await evaluate('!!document.querySelector("#sanitize-check strong")'),true);assert.equal(await evaluate('!!document.querySelector("#sanitize-check script, #sanitize-check svg, #sanitize-check [onclick], #sanitize-check [onerror], #sanitize-check a[href^=javascript]")'),false);
  console.log('PASS desktop/mobile, calendar selection, deep link, matching sidebar/policy, gallery, homepage links, safe rich text');
  await send('Browser.close');
})().catch(error=>{console.error(error);process.exit(1);});
