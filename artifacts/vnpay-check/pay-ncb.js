if(location.hostname!=='sandbox.vnpayment.vn')throw Error('Unexpected host');
document.getElementById('otpvalue').value='123456';
document.getElementById('otpvalue').dispatchEvent(new Event('input',{bubbles:true}));
document.getElementById('btnConfirm').click();
true;
