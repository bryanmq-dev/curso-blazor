namespace sharpness_sharp.Data.Entities;

public class ReservationStatusHistory
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? ChangedByUserId { get; set; }
    public ApplicationUser? ChangedByUser { get; set; }
}
