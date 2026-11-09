const fs=require('node:fs'),path=require('node:path');
const root='D:/TourismMvc/TourismMvc';let english=JSON.parse(fs.readFileSync(path.join(root,'Localization/user.en.json'),'utf8'));
const edit=(file,changes)=>{const target=path.join(root,file);let source=fs.readFileSync(target,'utf8');for(const [from,to] of changes){if(!source.includes(from))throw Error(file+': missing '+from.slice(0,120));source=source.split(from).join(to)}fs.writeFileSync(target,source)};
edit('Views/Shared/_UserLayout.cshtml',[
 ['<html lang="vi">','<html lang="@L.Language">'],
 ['</head><body','<link rel="stylesheet" href="~/css/user-language.css" asp-append-version="true" /></head><body'],
 ['<partial name="_UserAccountMenu" />','<partial name="_UserLanguageSelector" /><partial name="_UserAccountMenu" />'],
 ['<script src="https://unpkg.com/lucide','<partial name="_UserTranslations" /><script src="https://unpkg.com/lucide']
]);
edit('Views/Home/Index.cshtml',[
 ['Tìm thấy @Model.ResultCount chuyến đi.','@L["Tìm thấy {0} chuyến đi.", Model.ResultCount]'],
 ['Hiển thị @Model.SearchResults.Count chuyến đi mới nhất.','@L["Hiển thị {0} chuyến đi mới nhất.", Model.SearchResults.Count]'],
 ['Chưa có điểm đến tại @label.','@L["Chưa có điểm đến tại {0}.", label]']
]);
edit('Views/Home/_HomeTourCard.cshtml',[
 ['Tiết kiệm @','@L["Tiết kiệm"] @'],
 ['Còn @Model.AvailableSlots chỗ','@L["Còn {0} chỗ", Model.AvailableSlots!.Value]']
]);
edit('Views/PublicTours/Index.cshtml',[
 ['@PublicTourSearchViewModel.Budgets.First(b => b.Value == f.Budget).Label','@L[PublicTourSearchViewModel.Budgets.First(b => b.Value == f.Budget).Label]'],
 ['@f.Transportation <i','@L[f.Transportation] <i'],
 ['Từ @(Model.StartLocations.FirstOrDefault(d => d.Id == f.StartLocationID)?.Name ?? L["điểm đã chọn"])','@L["Từ {0}", Model.StartLocations.FirstOrDefault(d => d.Id == f.StartLocationID)?.Name ?? L["điểm đã chọn"]]'],
 ['@budget.Label','@L[budget.Label]'],['/>@transport</label>','/>@L[transport]</label>'],
 ['@tour.Transportation</span>','@L[tour.Transportation]</span>'],
 ['aria-label="Xem chi tiết @tour.Name"','aria-label="@L["Xem chi tiết {0}", tour.Name]"'],
 ['Hiển thị @((f.Page - 1) * Model.PageSize + 1)–@Math.Min(f.Page * Model.PageSize, Model.TotalItems) trên @Model.TotalItems chuyến đi','@L["Hiển thị {0}–{1} trên {2} chuyến đi", (f.Page - 1) * Model.PageSize + 1, Math.Min(f.Page * Model.PageSize, Model.TotalItems), Model.TotalItems]']
]);
edit('Views/PublicTours/Details.cshtml',[
 ['aria-label="Xem ảnh @(i + 1)"','aria-label="@L["Xem ảnh {0}", i + 1]"'],
 ['alt="@t.TourName - ảnh @(i + 1)"','alt="@L["{0} - ảnh {1}", t.TourName, i + 1]"'],
 ['aria-label="Tháng @month.Key.Month năm @month.Key.Year"','aria-label="@L["Tháng {0} năm {1}", month.Key.Month, month.Key.Year]"'],
 ['<strong>THÁNG @month.Key.Month/@month.Key.Year</strong>','<strong>@first.ToString("MMMM yyyy", L.Culture)</strong>'],
 ['{ <span>@label</span> }','{ <span>@L[label]</span> }'],
 ['+ "tr"','+ L["tr"]'],
 ['aria-label="Chọn lịch @departure.DepartureDate.ToString(L.DateTimeFormat, L.Culture), @Money(price), còn @departure.AvailableSlots chỗ"','aria-label="@L["Chọn lịch {0}, {1}, còn {2} chỗ", departure.DepartureDate.ToString(L.DateTimeFormat, L.Culture), Money(price), departure.AvailableSlots]"'],
 ['<h3>Khởi hành @','<h3>@L["Khởi hành"] @'],
 ['@price.AgeDescription','@L[price.AgeDescription]'],
 ['Ngày @item.DayNumber: @item.Title','@L["Ngày {0}: {1}", item.DayNumber, item.Title]'],
 ['Lưu trú: @item.Accommodation','@L["Lưu trú:"] @item.Accommodation'],
 ['>Giá từ: @if','>@L["Giá từ:"] @if'],
 ['>Giá người lớn: @if','>@L["Giá người lớn:"] @if'],
 ['<p>Khởi hành: @related.StartLocation','<p>@L["Khởi hành:"] @related.StartLocation']
]);
edit('Views/Booking/Order.cshtml',[
 ['@L["GỬI YÊU CẦU"]','@L["Thanh toán"]'],['@L["CHỜ XÁC NHẬN"]','@L["Xác nhận"]'],
 ['aria-label="Giảm số @label"','aria-label="@L["Giảm số {0}", label]"'],
 ['aria-label="Thêm @label"','aria-label="@L["Thêm {0}", label]"'],
 ['Còn @departure.AvailableSlots chỗ · Đang chọn','@L["Còn {0} chỗ · Đang chọn", departure.AvailableSlots]'],
 ['aria-label="Xoá hành khách @(i + 1)"','aria-label="@L["Xoá hành khách {0}", i + 1]"'],
 ['>@gender</option>','>@L[gender]</option>'],
 ['<option>Nam</option><option>@L["Nữ"]</option><option>@L["Khác"]</option>','<option value="Nam">@L["Nam"]</option><option value="Nữ">@L["Nữ"]</option><option value="Khác">@L["Khác"]</option>']
]);
edit('Views/Booking/Complete.cshtml',[
 ['<p>Ngày khởi hành: @','<p>@L["Ngày khởi hành:"] @'],
 ['Hành khách: @b.TotalPassengers người','@L["Hành khách: {0} người", b.TotalPassengers]'],
 [' (giờ Việt Nam)',' @L["(giờ Việt Nam)"]'],
 ['<p>Mã giao dịch VNPAY: @','<p>@L["Mã giao dịch VNPAY:"] @'],
 ['<p>Ngân hàng: @','<p>@L["Ngân hàng:"] @'],
 ['<p>Thời gian thanh toán: @','<p>@L["Thời gian thanh toán:"] @'],
 ['<p>Ghi chú: @','<p>@L["Ghi chú:"] @']
]);
edit('wwwroot/js/booking.js',[
 ['`Xoá ${counter?.dataset.label || tr(\'hành khách\')} ${numbers[type]}`',"tr('Xoá {0} {1}', counter?.dataset.label || tr('hành khách'), numbers[type])"]
]);
// Translate remaining complete text nodes using the catalogue, never user-entered content or attribute values.
for(const folder of ['Home','PublicTours','Booking','Account','Shared'])for(const name of fs.readdirSync(path.join(root,'Views',folder))){
 if(!name.endsWith('.cshtml')||folder==='Shared'&&!['_UserLayout.cshtml','_UserAccountMenu.cshtml'].includes(name))continue;
 const target=path.join(root,'Views',folder,name);let source=fs.readFileSync(target,'utf8');
 source=source.replace(/>([^<>]+)</g,(all,text)=>{const key=text.trim().replace(/&amp;/g,'&').replace(/\s+/g,' ');return !text.includes('@')&&english[key]?'>'+text.match(/^\s*/)[0]+'@L['+JSON.stringify(key)+']'+text.match(/\s*$/)[0]+'<':all});
 fs.writeFileSync(target,source);
}
console.log('Wired the selector, server/client translations, dates, counts and stable gender values.');
