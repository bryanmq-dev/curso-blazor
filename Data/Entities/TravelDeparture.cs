namespace sharpness_sharp.Data.Entities;

public class TravelDeparture
{
    public int Id { get; set; }
    public int TravelPackageId { get; set; }
    public TravelPackage? TravelPackage { get; set; }
    public DateTime DepartureDate { get; set; }
    public DateTime ReturnDate { get; set; }
    public int Capacity { get; set; }
    public int ReservedSeats { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    public int AvailableSeats => Capacity - ReservedSeats;
    public List<Reservation> Reservations { get; set; } = [];
}
