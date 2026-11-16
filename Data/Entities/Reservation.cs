using sharpness_sharp.Data.Enums;

namespace sharpness_sharp.Data.Entities;

public class Reservation
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public int TravelDepartureId { get; set; }
    public TravelDeparture? TravelDeparture { get; set; }
    public int NumberOfPeople { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pendiente;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Observations { get; set; }

    public List<ReservationStatusHistory> StatusHistory { get; set; } = [];
}
