const fs=require('node:fs'),path=require('node:path');
const root='D:/TourismMvc/TourismMvc',out=__dirname,keys=new Map();
const vi=/[À-ỹĐđ]/;
const add=(text,file)=>{const key=text.replace(/&amp;/g,'&').trim().replace(/\s+/g,' ');if(key&&!keys.has(key))keys.set(key,new Set());if(key)keys.get(key).add(file);return key};
const quote=text=>JSON.stringify(text);
const files=['Home','PublicTours','Booking','Account'].flatMap(folder=>fs.readdirSync(path.join(root,'Views',folder)).filter(n=>n.endsWith('.cshtml')).map(n=>'Views/'+folder+'/'+n));
files.push('Views/Shared/_UserLayout.cshtml','Views/Shared/_UserAccountMenu.cshtml');
for(const file of files){
 const target=path.join(root,file);let source=fs.readFileSync(target,'utf8');fs.mkdirSync(path.join(out,'before',path.dirname(file)),{recursive:true});fs.writeFileSync(path.join(out,'before',file),source);
 const protectedValues=[];const protect=value=>{const id=protectedValues.length;protectedValues.push(value);return '__USER_LOC_PROTECTED_'+id+'__'};
 // Gender values are a database contract. Localize only their visible labels below.
 source=source.replace(/new\[\]\s*\{\s*"Nam",\s*"Nữ",\s*"Khác"\s*\}/g,protect);
 source=source.replace(/@?L\["(?:\\.|[^"\\])*"\]/g,protect);
 // Static attributes; route/enum values are deliberately left as their original values.
 source=source.replace(/\b(placeholder|title|aria-label|alt|content)="([^"@<>]*)"/g,(all,name,value)=>vi.test(value)?name+'="'+protect('@L['+quote(add(value,file))+']')+'"':all);
 source=source.replace(/>([^<>]+)</g,(all,text)=>{
  if(!vi.test(text)||text.includes('@')||/[;{}()[\]="?]/.test(text)||text.includes('__USER_LOC_PROTECTED_'))return all;
  const leading=text.match(/^\s*/)[0],trailing=text.match(/\s*$/)[0];return '>'+leading+protect('@L['+quote(add(text,file))+']')+trailing+'<';
 });
 source=source.replace(/\$"((?:\\.|[^"\\])*)"/g,(all,value)=>{
  if(!vi.test(value)||value.includes('@'))return all;
  const arguments=[];const template=value.replace(/\{([^{}]+)\}/g,(_,expression)=>{arguments.push(expression);return '{'+(arguments.length-1)+'}'});
  return protect('L['+quote(add(template,file))+(arguments.length?', '+arguments.join(', '):'')+']');
 });
 source=source.replace(/"((?:\\.|[^"\\])*)"/g,(all,value)=>{
  if(!vi.test(value)||value.includes('@')||value.includes('__USER_LOC_PROTECTED_')||value.includes('<'))return all;
  let decoded;try{decoded=JSON.parse(all)}catch{return all}
  return protect('L['+quote(add(decoded,file))+']');
 });
 source=source.replace(/__USER_LOC_PROTECTED_(\d+)__/g,(_,id)=>protectedValues[id]);
 source=source.replace('const string description = L[','var description = L[');
 source=source.replace(/System\.Globalization\.CultureInfo\.GetCultureInfo\("vi-VN"\)/g,'L.Culture');
 source=source.replace(/\.ToString\("dd\/MM\/yyyy HH:mm"\)/g,'.ToString(L.DateTimeFormat, L.Culture)').replace(/\.ToString\("HH:mm dd\/MM\/yyyy"\)/g,'.ToString(L.TimeDateFormat, L.Culture)').replace(/\.ToString\("dd\/MM\/yyyy"\)/g,'.ToString(L.DateFormat, L.Culture)');
 fs.writeFileSync(target,source);
}
for(const file of ['wwwroot/js/booking.js','wwwroot/js/auth.js','wwwroot/js/payment.js','wwwroot/js/tour-detail.js']){
 const target=path.join(root,file);let source=fs.readFileSync(target,'utf8');fs.mkdirSync(path.join(out,'before',path.dirname(file)),{recursive:true});fs.writeFileSync(path.join(out,'before',file),source);
 source=source.replace(/'(?:\\.|[^'\\])*'|"(?:\\.|[^"\\])*"/g,all=>{
  if(!vi.test(all))return all;
  const text=all.slice(1,-1);add(text,file);return 'tr('+all+')';
 });
 source=source.replace("'use strict';","'use strict';\n    const tr = (...args) => window.UserI18n?.t(...args) ?? args[0];");
 if(!source.includes('const tr ='))source=source.replace('(() => {','(() => {\n    const tr = (...args) => window.UserI18n?.t(...args) ?? args[0];');
 source=source.replace("new Intl.NumberFormat('vi-VN')","new Intl.NumberFormat(window.UserI18n?.locale || 'en-GB')");
 fs.writeFileSync(target,source);
}
for(const file of ['Models/ViewModels/AuthViewModels.cs','Models/ViewModels/BookingViewModels.cs','Models/ViewModels/PublicTourSearchViewModels.cs','Services/AccountService.cs','Services/BookingOrderService.cs','Services/VnpayPaymentService.cs','Controllers/AccountController.cs','Controllers/BookingController.cs','Controllers/PublicToursController.cs','Controllers/VnpayController.cs']){
 const source=fs.readFileSync(path.join(root,file),'utf8');
 for(const match of source.matchAll(/"((?:\\.|[^"\\])*)"/g))if(vi.test(match[1])){
  let text;try{text=JSON.parse(match[0])}catch{continue}
  if(source[match.index-1]==='$'){let index=0;text=text.replace(/\{[^{}]+\}/g,()=>'{'+index+++'}');}
  add(text,file);
 }
}
add('Ngôn ngữ','selector');
fs.writeFileSync(path.join(root,'Localization/user.en.json'),JSON.stringify(Object.fromEntries([...keys.keys()].map(k=>[k,''])),null,2)+'\n');
fs.writeFileSync(path.join(out,'inventory.json'),JSON.stringify([...keys].map(([key,files])=>({key,files:[...files]})),null,2));
console.log('Prepared '+files.length+' Razor views and '+keys.size+' translation keys.');
