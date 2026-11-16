namespace sharpness_sharp.Data.Entities;

public class TravelImage
{
    public int Id { get; set; }
    public int TravelPackageId { get; set; }
    public TravelPackage? TravelPackage { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}
