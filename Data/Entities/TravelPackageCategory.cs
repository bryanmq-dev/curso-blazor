namespace sharpness_sharp.Data.Entities;

public class TravelPackageCategory
{
    public int TravelPackageId { get; set; }
    public TravelPackage? TravelPackage { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}
