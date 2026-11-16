namespace sharpness_sharp.Data.Entities;

public class ItineraryDay
{
    public int Id { get; set; }
    public int TravelPackageId { get; set; }
    public TravelPackage? TravelPackage { get; set; }
    public int DayNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
