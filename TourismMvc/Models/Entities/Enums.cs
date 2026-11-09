namespace TourismMvc.Models.Entities;

// Lưu trong DB dạng chuỗi (EnumType.STRING) – cấu hình ở AppDbContext.
// New online reservations: PENDING_PAYMENT -> PAID | EXPIRED. Legacy bookings keep PENDING_CONFIRMATION.
public enum BookingStatus { PENDING_CONFIRMATION, PAID, CANCELLED, PENDING_PAYMENT, EXPIRED }
public enum PassengerType { ADULT, CHILD, INFANT, SINGLE_SUPPLEMENT }
public enum Region { NORTH, CENTRAL, SOUTH }
public enum Role { ADMIN, CUSTOMER, TOUR_OWNER }
public enum TransportType { OUTBOUND, INBOUND }
public enum VehicleType { PLANE, BUS, TRAIN, SHIP, CAR }
