const fs=require('node:fs'),path=require('node:path');
for(const folder of ['Home','PublicTours','Booking','Account','Shared'])for(const name of fs.readdirSync('TourismMvc/Views/'+folder)){
 if(!name.endsWith('.cshtml')||folder==='Shared'&&!['_UserLayout.cshtml','_UserAccountMenu.cshtml'].includes(name))continue;
 fs.readFileSync('TourismMvc/Views/'+folder+'/'+name,'utf8').split('\n').forEach((line,i)=>{
  const visible=line.replace(/L\["(?:\\.|[^"\\])*"/g,'L[').replace(/"(?:\\.|[^"\\])*"/g,'');
  if(/[À-ỹĐđ]/.test(visible))console.log(folder+'/'+name+':'+(i+1)+' '+visible.trim());
 });
}
