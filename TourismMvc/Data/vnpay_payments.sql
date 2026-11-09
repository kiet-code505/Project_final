-- Additive migration. Existing bookings keep their status and have no automatic expiry.
BEGIN;
ALTER TABLE bookings ADD COLUMN IF NOT EXISTS payment_deadline timestamp without time zone;
ALTER TABLE bookings ADD COLUMN IF NOT EXISTS reserved_seats integer;
ALTER TABLE bookings ADD COLUMN IF NOT EXISTS seats_released boolean NOT NULL DEFAULT false;
ALTER TABLE bookings DROP CONSTRAINT IF EXISTS bookings_booking_status_check;
ALTER TABLE bookings ADD CONSTRAINT bookings_booking_status_check CHECK
    (booking_status IN ('PENDING_CONFIRMATION','PENDING_PAYMENT','PAID','CANCELLED','EXPIRED'));

CREATE TABLE IF NOT EXISTS mvc_vnpay_payments (
    id bigserial PRIMARY KEY,
    booking_id integer NOT NULL REFERENCES bookings(bookingid) ON DELETE RESTRICT,
    transaction_ref varchar(40) NOT NULL UNIQUE,
    merchant_code varchar(32) NOT NULL,
    amount numeric(18,2) NOT NULL CHECK (amount > 0),
    status varchar(24) NOT NULL CHECK (status IN ('PENDING','SUCCEEDED','FAILED','EXPIRED','REVIEW_REQUIRED')),
    created_at timestamp without time zone NOT NULL,
    expires_at timestamp without time zone NOT NULL,
    last_checked_at timestamp without time zone,
    settled_at timestamp without time zone,
    provider_transaction_no varchar(100),
    bank_code varchar(50),
    response_code varchar(10),
    transaction_status varchar(10)
);
CREATE INDEX IF NOT EXISTS ix_mvc_vnpay_booking ON mvc_vnpay_payments(booking_id);
CREATE INDEX IF NOT EXISTS ix_mvc_vnpay_pending ON mvc_vnpay_payments(created_at) WHERE status = 'PENDING';
CREATE INDEX IF NOT EXISTS ix_bookings_payment_deadline ON bookings(payment_deadline) WHERE booking_status = 'PENDING_PAYMENT';
COMMIT;
