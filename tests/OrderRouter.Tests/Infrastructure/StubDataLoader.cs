using OrderRouter.Api.Models;
using OrderRouter.Api.Services;

namespace OrderRouter.Tests.Infrastructure;

/// <summary>
/// Shared test data loader that allows test cases to easily define suppliers and products without relying on external files.
/// </summary>
/// <param name="suppliers"></param>
public class StubDataLoader(params Supplier[] suppliers) : IDataLoader
{
    private readonly List<Supplier> _suppliers = [.. suppliers];
    private readonly Dictionary<string, Product> _products = new(StringComparer.OrdinalIgnoreCase);

    public void AddProduct(string code, string category)
    {
        _products[code] = new Product { ProductCode = code, ProductName = code, Category = category };
    }

    public List<Supplier> Suppliers => _suppliers;
    public Dictionary<string, Product> Products => _products;
}