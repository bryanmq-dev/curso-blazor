namespace sharpness_sharp.Data.Entities;

public class TravelPackage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DestinationId { get; set; }
    public Destination? Destination { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int DurationNights { get; set; }
    public string MainImage { get; set; } = string.Empty;
    public string IncludedServices { get; set; } = string.Empty;
    public string NotIncludedServices { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public List<TravelPackageCategory> TravelPackageCategories { get; set; } = [];
    public List<TravelImage> Images { get; set; } = [];
    public List<ItineraryDay> ItineraryDays { get; set; } = [];
    public List<TravelDeparture> Departures { get; set; } = [];
}
