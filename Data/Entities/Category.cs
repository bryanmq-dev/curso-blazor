namespace sharpness_sharp.Data.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public List<TravelPackageCategory> TravelPackageCategories { get; set; } = [];
}
