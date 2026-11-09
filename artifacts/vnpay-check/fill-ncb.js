if(location.hostname!=='sandbox.vnpayment.vn')throw Error('Unexpected host');
for(const [id,value] of Object.entries({card_number_mask:'9704198526191432198',cardNumber:'9704198526191432198',cardHolder:'NGUYEN VAN A',cardDate:'07/15'})){
 const input=document.getElementById(id);input.value=value;input.dispatchEvent(new Event('input',{bubbles:true}));input.dispatchEvent(new Event('change',{bubbles:true}));
}
JSON.stringify({links:[...document.querySelectorAll('a')].filter(x=>/Tiếp tục|Hủy|Điều kiện/.test(x.textContent)).map(x=>({id:x.id,text:x.textContent.trim(),href:x.getAttribute('href'),onclick:x.getAttribute('onclick')})),forms:[...document.forms].map(x=>({id:x.id,action:new URL(x.action).pathname}))});
