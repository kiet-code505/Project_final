namespace TourismMvc.Models.Entities;

// Lưu trong DB dạng chuỗi (EnumType.STRING) – cấu hình ở AppDbContext.
// Luồng booking: PENDING_CONFIRMATION -> PAID | CANCELLED
public enum BookingStatus { PENDING_CONFIRMATION, PAID, CANCELLED }
public enum PassengerType { ADULT, CHILD, INFANT, SINGLE_SUPPLEMENT }
public enum Region { NORTH, CENTRAL, SOUTH }
public enum Role { ADMIN, CUSTOMER, TOUR_OWNER }
public enum TransportType { OUTBOUND, INBOUND }
public enum VehicleType { PLANE, BUS, TRAIN, SHIP, CAR }
