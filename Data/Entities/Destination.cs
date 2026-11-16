namespace sharpness_sharp.Data.Entities;

public class Destination
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string MainImage { get; set; } = string.Empty;
    public string GalleryImages { get; set; } = string.Empty;
    public string TouristInfo { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public List<TravelPackage> TravelPackages { get; set; } = [];
}
