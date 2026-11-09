-- =====================================================================
-- Dọn DB "tourism" (PostgreSQL) để chỉ giữ lại các bảng/cột mà TourismMvc dùng.
-- Đối chiếu với Models/Entities + Data/AppDbContext.cs.
-- Chạy trong 1 transaction: lỗi ở bất kỳ bước nào sẽ rollback toàn bộ.
-- =====================================================================
BEGIN;

-- ---------------------------------------------------------------------
-- 1. XOÁ BẢNG không có entity trong TourismMvc
-- ---------------------------------------------------------------------
DROP TABLE IF EXISTS review_images      CASCADE;
DROP TABLE IF EXISTS reviews            CASCADE;
DROP TABLE IF EXISTS refund_information CASCADE;
DROP TABLE IF EXISTS payments           CASCADE;
DROP TABLE IF EXISTS refresh_tokens     CASCADE;
DROP TABLE IF EXISTS tour_media         CASCADE;
DROP TABLE IF EXISTS coupons            CASCADE;

-- ---------------------------------------------------------------------
-- 2. XOÁ CỘT không có property tương ứng
-- ---------------------------------------------------------------------
-- users
ALTER TABLE users
    DROP COLUMN IF EXISTS coin_balance,
    DROP COLUMN IF EXISTS date_of_birth,
    DROP COLUMN IF EXISTS avatar,
    DROP COLUMN IF EXISTS district_code,
    DROP COLUMN IF EXISTS district_name,
    DROP COLUMN IF EXISTS province_code,
    DROP COLUMN IF EXISTS province_name,
    DROP COLUMN IF EXISTS last_active_at,
    DROP COLUMN IF EXISTS verification_token,
    DROP COLUMN IF EXISTS verification_token_expiry;

-- bookings (paid_by_coin, coupon_discount là NOT NULL -> nếu giữ lại EF insert sẽ lỗi)
ALTER TABLE bookings
    DROP COLUMN IF EXISTS cancel_reason,
    DROP COLUMN IF EXISTS paid_by_coin,
    DROP COLUMN IF EXISTS refund_amount,
    DROP COLUMN IF EXISTS surcharge,
    DROP COLUMN IF EXISTS applied_coupon_codes,
    DROP COLUMN IF EXISTS coupon_discount;

-- booking_passengers
ALTER TABLE booking_passengers
    DROP COLUMN IF EXISTS requires_single_room,
    DROP COLUMN IF EXISTS single_room_surcharge;

-- tour_departures (FK tới coupons đã bị CASCADE xoá ở bước 1)
ALTER TABLE tour_departures
    DROP COLUMN IF EXISTS coupon_id;

-- policy_templates ("update_at" là cột trùng do gõ sai, MVC dùng "updated_at")
ALTER TABLE policy_templates
    DROP COLUMN IF EXISTS child_pricing_notes,
    DROP COLUMN IF EXISTS force_majeure_rules,
    DROP COLUMN IF EXISTS holiday_cancellation_rules,
    DROP COLUMN IF EXISTS packing_list,
    DROP COLUMN IF EXISTS registration_conditions,
    DROP COLUMN IF EXISTS status,
    DROP COLUMN IF EXISTS update_at;

-- branch_contacts
ALTER TABLE branch_contacts
    DROP COLUMN IF EXISTS is_head_office,
    DROP COLUMN IF EXISTS update_at;

-- ---------------------------------------------------------------------
-- 3. ĐỒNG BỘ ENUM BookingStatus (MVC chỉ có PENDING_CONFIRMATION | PAID | CANCELLED)
--    Dữ liệu cũ có giá trị khác sẽ làm EF Core ném lỗi khi đọc.
-- ---------------------------------------------------------------------
UPDATE bookings SET booking_status = 'PENDING_CONFIRMATION'
 WHERE booking_status IN ('PENDING_PAYMENT', 'OVERDUE_PAYMENT', 'PENDING_REVIEW') OR booking_status IS NULL;
UPDATE bookings SET booking_status = 'PAID'      WHERE booking_status = 'REVIEWED';
UPDATE bookings SET booking_status = 'CANCELLED' WHERE booking_status = 'PENDING_REFUND';

ALTER TABLE bookings DROP CONSTRAINT IF EXISTS bookings_booking_status_check;
ALTER TABLE bookings ADD  CONSTRAINT bookings_booking_status_check
    CHECK (booking_status IN ('PENDING_CONFIRMATION', 'PAID', 'CANCELLED'));

COMMIT;
