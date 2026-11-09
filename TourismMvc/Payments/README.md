# VNPAY Sandbox trong TourismMvc

Luồng: đặt tour → giữ chỗ và tạo booking `PENDING_PAYMENT` → khách bấm VNPAY → tạo hoặc dùng lại giao dịch đang chờ → VNPAY Sandbox → IPN hoặc truy vấn server xác nhận → `PAID`.

## Cấu hình

- Giá trị chung ở `appsettings.Vnpay.json`: `PublicBaseUrl`, thời gian giữ chỗ (mặc định 15 phút), thời gian chờ đối soát trước khi hoàn chỗ (mặc định 2 phút).
- Thông tin merchant ở `appsettings.Vnpay.Development.local.json` (được gitignore và không đưa vào bản publish). Cấu trúc:

```json
{
  "Vnpay": {
    "TmnCode": "MA_WEBSITE_SANDBOX_DA_DUOC_DUYET",
    "HashSecret": "KHOA_BI_MAT_SANDBOX"
  }
}
```

- Có thể thay bằng biến môi trường `Vnpay__TmnCode`, `Vnpay__HashSecret`, `Vnpay__PublicBaseUrl`. Biến môi trường ghi đè file cấu hình. Không lưu khóa bí mật trong mã nguồn hoặc gửi vào chat.
- Khởi động lại ứng dụng sau khi thay thông tin merchant. Tích hợp này dùng cố định cổng và API Sandbox, chưa thực hiện thanh toán thật.
- Cấu hình cũ từ source Java từng bị VNPAY trả **“Website này chưa được phê duyệt”**. Ngày 31/10/2026, đã nạp bộ thông tin mới của người dùng. Sau khi khai báo IPN public trong hệ thống SIT, đã kiểm thử thành công giao dịch NCB Sandbox 10.000 VND: VNPAY trả mã `00/00`, booking tự chuyển `PAID`, giao dịch lưu `SUCCEEDED` với mã VNPAY thật của môi trường Sandbox, số chỗ chỉ trừ một lần. Ảnh: `artifacts/vnpay-check/actual-vnpay-paid.png`. Các bản ghi fixture local đã được dọn sau kiểm tra; giao dịch thử vẫn có thể xem trên hệ thống Sandbox VNPAY.
- Trong lần kiểm tra trước khi cấu hình IPN, API `querydr` trả mã `99 / Unexpected error`; chưa xác nhận riêng tình trạng API này sau đó. Luồng thanh toán có IPN được khai báo đã kiểm thử thành công. Chỉ đổi booking thành `PAID` khi có kết quả đã xác thực từ IPN hoặc truy vấn. Có thể đăng ký merchant ở https://sandbox.vnpayment.vn/devreg/ và làm theo email hướng dẫn của VNPAY.
- API Sandbox trả HTTP 403 nếu request không có User-Agent trong lần kiểm tra này. HttpClient của ứng dụng đã thêm `TourismMvc/1.0`; lỗi HTTP và mã lỗi truy vấn được ghi log mà không ghi khóa bí mật hoặc payload đã ký.

## URL

- Website mặc định: `http://localhost:5019`.
- Return URL: `{PublicBaseUrl}/payment/vnpay/return`.
- IPN URL: `{PublicBaseUrl}/payment/vnpay/ipn` — khai báo tại cấu hình merchant Sandbox. Để VNPAY gọi IPN, địa chỉ này phải truy cập được từ Internet (HTTPS khi triển khai). Nếu dùng tunnel, đặt `PublicBaseUrl` thành URL tunnel và mở cả luồng đặt tour qua URL đó để giữ cookie đúng trình duyệt/domain.
- Khi localhost không nhận được IPN, trang kết quả và nút “Kiểm tra thanh toán” truy vấn trực tiếp API VNPAY `querydr`. Một redirect thành công từ trình duyệt không tự đổi booking thành `PAID`.
- Admin: `/admin/payments`, có danh sách giao dịch, lọc trạng thái và kiểm tra lại với VNPAY.
- Đã chuẩn bị cầu nối IPN riêng tại `artifacts/ipn-tunnel` với Quick Tunnel. URL hiện tại lưu trong `active-endpoint.json`. Khi dùng cầu nối này, giữ Return URL/PublicBaseUrl ở localhost và chỉ khai báo URL tunnel làm IPN trong VNPAY; xem README của cầu nối. Người dùng đã hoàn tất khai báo trên hệ thống SIT và luồng thanh toán đã được kiểm thử thành công.

## Database

Đã áp dụng `Data/vnpay_payments.sql` vào database đang dùng. Với database khác, chạy script này trước khi khởi động phiên bản mới; có thể chạy lại. Script bổ sung cột cho booking, bảng `mvc_vnpay_payments`, index và mở rộng CHECK constraint trạng thái. Các booking cũ không được gán hạn thanh toán tự động, không bị tác vụ nền hủy.

## Xác thực và giữ chỗ

- Số tiền lấy từ booking trong database, không lấy từ form. Request VNPAY có số tiền VND nhân 100; chữ ký HMAC-SHA512 theo canonical query đã URL-encode của VNPAY 2.1.0.
- IPN kiểm tra chữ ký constant-time, merchant, transaction reference, số tiền, mã kết quả và trạng thái giao dịch. Truy vấn `querydr` kiểm tra chữ ký phản hồi riêng theo đặc tả.
- Tạo thanh toán cần quyền đọc booking (tài khoản sở hữu hoặc receipt cookie của khách vãng lai) và antiforgery token. IPN/Return chỉ nhận dữ liệu VNPAY đã ký, không cần cookie đăng nhập.
- Lock departure → booking → payment trong transaction. Booking giữ chỗ khi tạo; thanh toán thành công không trừ chỗ lần nữa. Hai lần bấm thanh toán dùng lại cùng giao dịch đang chờ. Giao dịch thất bại có thể tạo lần thử mới khi còn hạn.
- Tác vụ nền chạy mỗi phút, truy vấn giao dịch đang chờ và xử lý hết hạn. Chỉ hoàn số chỗ đã giữ (không tính em bé) một lần. Sau khi khởi động lại, tác vụ xử lý cả đơn đã quá hạn.
- Giao dịch thành công tới sau khi chỗ đã hoàn lại, sau hạn giao dịch hoặc khi booking đã có một khoản thanh toán khác: lưu `REVIEW_REQUIRED`, không tự xác nhận thêm chỗ. Admin phải đối soát để phục vụ khách hoặc hoàn tiền. Chưa tích hợp API hoàn tiền tự động.
- Giao dịch đã hết hạn tiếp tục được truy vấn trong 24 giờ để phát hiện tiền đến chậm; IPN có chữ ký vẫn được kiểm tra nếu đến muộn hơn.

## Kiểm thử

`artifacts/vnpay-check` chứa kiểm thử HMAC theo RFC 4231, canonical query, PostgreSQL transaction/concurrency, callback giả mạo/sai tiền/trùng, thử lại sau thất bại, hết hạn/hoàn chỗ, tiền đến muộn, server reconciliation, HTTP ownership/CSRF và ảnh desktop/mobile. Fixture chỉ dùng tour/tài khoản tạm riêng và được xóa sau kiểm tra.

Thẻ thử chính thức: ngân hàng NCB, số thẻ `9704198526191432198`, tên `NGUYEN VAN A`, ngày phát hành `07/15`, OTP `123456`. Chỉ dùng tại VNPAY Sandbox.

Tài liệu chính thức:
- https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html
- https://sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr%26refund.html
- https://sandbox.vnpayment.vn/apis/vnpay-demo/
