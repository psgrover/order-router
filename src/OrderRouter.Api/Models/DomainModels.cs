namespace OrderRouter.Api.Models;

/// <summary>
/// Supplier represents a supplier's capabilities and ratings.
/// </summary>
public class Supplier
{
    public string SupplierId { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public HashSet<string> ServiceZips { get; set; } = [];
    public HashSet<string> ProductCategories { get; set; } = [];
    public double? SatisfactionScore { get; set; }   // null = no ratings yet
    public bool CanMailOrder { get; set; }

    /// <summary>Returns true if this supplier covers the given ZIP code.</summary>
    public bool ServesZip(string zip) => ServiceZips.Contains(zip.Trim());
}

/// <summary>
/// Represents a product with identifying code, name, and category information.
/// </summary>
public class Product
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
