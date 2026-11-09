const fs = require('node:fs');
const assert = require('node:assert/strict');
const WebSocket = require('D:/Đồ án cuối kì/frontend/node_modules/ws');

(async () => {
  const tabs = await (await fetch('http://127.0.0.1:9255/json')).json();
  const ws = new WebSocket(tabs.find(t => t.type === 'page').webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { ws.once('open', resolve); ws.once('error', reject); });
  let sequence = 0;
  const pending = new Map(), errors = [];
  ws.on('message', data => {
    const message = JSON.parse(data);
    if (message.method === 'Runtime.exceptionThrown') errors.push(message.params.exceptionDetails.text);
    if (message.id) {
      const item = pending.get(message.id); pending.delete(message.id);
      message.error ? item.reject(message.error) : item.resolve(message.result);
    }
  });
  const send = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++sequence;
    const timeout = setTimeout(() => reject(Error('CDP timeout: ' + method)), 20000);
    pending.set(id, { resolve: value => { clearTimeout(timeout); resolve(value); }, reject: error => { clearTimeout(timeout); reject(error); } }); ws.send(JSON.stringify({ id, method, params }));
  });
  const evaluate = async expression => {
    const result = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw Error(JSON.stringify(result.exceptionDetails));
    return result.result.value;
  };
  const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
  const wait = async expression => {
    for (let i = 0; i < 120; i++) { if (await evaluate(expression)) return; await delay(100); }
    throw Error('Timeout: ' + expression);
  };
  const go = async page => {
    console.log('Checking ' + page);
    await send('Page.navigate', { url: 'http://127.0.0.1:5047/admin/localization-preview/' + page + '?search=sample&page=2#section' });
    await delay(400); await wait('document.readyState === "complete"');
  };
  const language = async code => {
    await evaluate(`document.querySelector('.user-language-selector button[value="${code}"]').click()`);
    await delay(400); await wait(`document.documentElement.lang === '${code}' && document.readyState === 'complete'`);
    assert.equal(await evaluate('location.search + location.hash'), '?search=sample&page=2#section');
  };
  await send('Page.enable'); await send('Runtime.enable');
  await send('Network.enable'); await send('Network.clearBrowserCookies');
  await send('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  await go('Locations/Index');
  assert.equal(await evaluate('document.documentElement.lang'), 'en');
  await evaluate('document.querySelector(".js-delete-one").click()');
  assert.equal(await evaluate('document.querySelector("#confirmTitle").textContent'), 'Delete "Nội dung mẫu"?');
  assert.equal(await evaluate('document.querySelector("#confirmMessage").textContent'), 'This action cannot be undone.');
  await language('vi');
  assert.equal(await evaluate('document.querySelector("h1").textContent'), 'Quản lý địa điểm');
  await language('en');
  await go('Departures/Index');
  await evaluate('document.querySelector("[data-departure-id]").click()');
  assert.equal(await evaluate('document.querySelector("[data-selected-count]").textContent'), '1 departures selected');
  await evaluate('document.querySelector("[data-delete-selected]").click()');
  assert.equal(await evaluate('document.querySelector("[data-delete-description]").textContent'), 'You are about to delete 1 departures with their pricing and transport. This action cannot be undone.');
  await go('Tours/Form');
  await evaluate('document.querySelector("#tabItinerary").click(); document.querySelector("#btnAddDay").click()');
  assert.equal(await evaluate('document.querySelectorAll("#dayList .js-day").length'), 2);
  assert.equal(await evaluate('[...document.querySelectorAll("#dayList .js-day")].at(-1).textContent.includes("Title")'), true);
  await go('Users/Index');
  fs.writeFileSync(__dirname + '/admin-en-desktop.png', Buffer.from((await send('Page.captureScreenshot', { format: 'png' })).data, 'base64'));
  for (const width of [390, 375]) {
    await send('Emulation.setDeviceMetricsOverride', { width, height: 844, deviceScaleFactor: 1, mobile: true });
    await go('Locations/Index');
    for (const code of ['vi', 'en']) {
      await language(code);
      assert.equal(await evaluate('document.querySelector(".user-language-selector").getBoundingClientRect().right <= innerWidth'), true, 'mobile selector fits');
      assert.equal(await evaluate('document.querySelector(".topbar").scrollWidth <= innerWidth'), true, 'mobile header fits');
    }
  }
  fs.writeFileSync(__dirname + '/admin-en-mobile.png', Buffer.from((await send('Page.captureScreenshot', { format: 'png' })).data, 'base64'));
  await go('Account/Login');
  await evaluate('document.querySelector("[data-toggle-password]").click()');
  assert.equal(await evaluate('document.querySelector("[data-toggle-password]").getAttribute("aria-label")'), 'Hide password');
  await language('vi');
  assert.equal(await evaluate('document.querySelector(".formTitle").textContent'), 'Đăng nhập Admin');
  assert.deepEqual(errors, [], 'no browser script errors');
  console.log('PASS browser: EN/VI persistence, URL filters and fragment, delete dialogs, selection labels, cloned itinerary, password toggle and mobile header at 375/390px');
  await send('Browser.close');
})().catch(error => { console.error(error); process.exit(1); });
