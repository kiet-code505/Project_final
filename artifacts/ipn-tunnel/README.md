# Cầu nối IPN VNPAY Sandbox

Tạo ngày 31/10/2026 để VNPAY có thể gọi endpoint IPN của TourismMvc đang chạy trên máy phát triển.

- TourismMvc: `http://localhost:5019`.
- Cầu nối: `http://127.0.0.1:5187`.
- URL public đang hoạt động: xem `active-endpoint.json`.
- Endpoint duy nhất được chuyển tiếp: `GET /payment/vnpay/ipn`. Các đường dẫn khác trả 404.
- Cầu nối không gửi cookie hay Authorization header đến MVC. Chữ ký, merchant, reference, số tiền và trạng thái được TourismMvc xác thực như trước.

Người dùng đã khai báo **IPN URL** trong `active-endpoint.json` trên hệ thống SIT VNPAY. Sau đó đã kiểm thử giao dịch NCB Sandbox thành công, booking tự chuyển thành `PAID`. Trang quản trị merchant và công cụ SIT là địa chỉ cấu hình/kiểm thử của VNPAY, không phải IPN URL của TourismMvc.

Giữ `Vnpay:PublicBaseUrl` là `http://localhost:5019` khi dùng cầu nối này: Return URL đưa trình duyệt trở lại web local, còn VNPAY gửi thông báo server qua IPN URL public riêng. Không thay PublicBaseUrl bằng URL của cầu nối vì cầu nối chỉ phục vụ IPN.

## Khởi động lại khi cần

TourismMvc phải chạy ở cổng 5019. Trong terminal thứ hai:

```powershell
dotnet run --project D:\TourismMvc\artifacts\ipn-tunnel\IpnBridge.csproj -c Release --no-launch-profile
```

Trong terminal thứ ba:

```powershell
D:\TourismMvc\artifacts\ipn-tunnel\cloudflared.exe tunnel --no-autoupdate --url http://127.0.0.1:5187 --protocol http2 --logfile D:\TourismMvc\artifacts\ipn-tunnel\tunnel.log
```

Cloudflared là bản portable chính thức từ GitHub `cloudflare/cloudflared`, đã kiểm tra SHA-256 với digest release. Không cài service hệ thống. Quick Tunnel có thể đổi URL mỗi lần khởi động, cần cập nhật IPN URL trong VNPAY tương ứng. Đây là cấu hình thử nghiệm; triển khai lâu dài dùng URL ổn định.

Đã kiểm tra URL public: request IPN không có chữ ký trả JSON mã 97, `/admin/users` trả 404. Sau khi người dùng khai báo URL, giao dịch thẻ NCB Sandbox trả mã `00/00`; trạng thái trong database tự cập nhật `PAID`/`SUCCEEDED`, mã giao dịch khớp với kết quả VNPAY. Chỗ chỉ được giữ một lần. Ảnh kiểm thử tại `artifacts/vnpay-check/actual-vnpay-paid.png`; dữ liệu fixture local đã được dọn.
