using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using OrderRouter.Api.Models;

namespace OrderRouter.Api.Services;

/// <summary>
/// Interface for loading supplier and product data from CSV files.
/// </summary>
public interface IDataLoader
{
    List<Supplier> Suppliers { get; }
    Dictionary<string, Product> Products { get; }
}

/// <summary>
/// Provides an implementation of the IDataLoader interface that loads supplier and product data from CSV files in a
/// specified directory.
/// </summary>
/// <remarks>The loader expects the files 'suppliers.csv' and 'products.csv' to be present in the specified
/// directory. Data is loaded when the object is constructed. The CSV files must have headers matching the expected
/// column names. The loader normalizes product categories to lowercase for case-insensitive matching. Supplier ZIP
/// codes are parsed to support explicit lists and ranges.</remarks>
/// <param name="dataDirectory">The path to the directory containing the CSV files to load. Must not be null or empty.</param>
public class CsvDataLoader(string dataDirectory) : IDataLoader
{
    public List<Supplier> Suppliers { get; } = LoadSuppliers(Path.Combine(dataDirectory, "suppliers.csv"));
    public Dictionary<string, Product> Products { get; } = LoadProducts(Path.Combine(dataDirectory, "products.csv"));

    // ── Products ──────────────────────────────────────────────────────────

    private static Dictionary<string, Product> LoadProducts(string path)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            HeaderValidated = null,
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, config);

        var dict = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);

        csv.Read(); csv.ReadHeader();
        while (csv.Read())
        {
            var code = csv.GetField("product_code")?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code)) continue;

            dict[code] = new Product
            {
                ProductCode = code,
                ProductName = csv.GetField("product_name")?.Trim() ?? string.Empty,
                // Normalize category: lowercase so matching is case-insensitive
                Category = (csv.GetField("category")?.Trim() ?? "").ToLowerInvariant(),
            };
        }
        return dict;
    }

    // ── Suppliers ─────────────────────────────────────────────────────────

    private static List<Supplier> LoadSuppliers(string path)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            HeaderValidated = null,
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, config);

        var list = new List<Supplier>();

        csv.Read(); csv.ReadHeader();
        while (csv.Read())
        {
            var id = csv.GetField("supplier_id")?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id)) continue;

            // The CSV header has a typo: "suplier_name" (one 'p')
            var name = csv.GetField("suplier_name")?.Trim()
                    ?? csv.GetField("supplier_name")?.Trim()
                    ?? string.Empty;

            var serviceZipsRaw = csv.GetField("service_zips")?.Trim() ?? string.Empty;
            var categoriesRaw  = csv.GetField("product_categories")?.Trim() ?? string.Empty;
            var scoreRaw       = csv.GetField("customer_satisfaction_score")?.Trim() ?? string.Empty;
            var mailOrderRaw   = csv.GetField("can_mail_order?")?.Trim() ?? string.Empty;

            var supplier = new Supplier
            {
                SupplierId        = id,
                SupplierName      = name,
                ServiceZips       = ParseZips(serviceZipsRaw),
                ProductCategories = ParseCategories(categoriesRaw),
                SatisfactionScore = ParseScore(scoreRaw),
                CanMailOrder      = mailOrderRaw.Equals("y", StringComparison.OrdinalIgnoreCase),
            };

            list.Add(supplier);
        }
        return list;
    }

    // ── ZIP parsing ───────────────────────────────────────────────────────

    /// <summary>
    /// Handles three formats found in the data:
    ///   1. Explicit list:  "10001, 10002, 10003"
    ///   2. Range:          "10001-10100"
    ///   3. Mixed:          "10451-10478, 10479-10502"
    /// A segment is a range iff both sides are all-digits and have equal length.
    /// Lone 5-digit strings are treated as explicit ZIPs.
    /// </summary>
    public static HashSet<string> ParseZips(string raw)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw)) return result;

        // Split on commas first
        foreach (var segment in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var s = segment.Trim();
            if (string.IsNullOrEmpty(s)) continue;

            // Try to interpret as a range: two numeric parts separated by '-'
            // Be careful: a ZIP like "10001" has no '-', but "10001-10100" does.
            var dashIdx = s.IndexOf('-');
            if (dashIdx > 0 && dashIdx < s.Length - 1)
            {
                var left  = s[..dashIdx].Trim();
                var right = s[(dashIdx + 1)..].Trim();

                if (IsNumeric(left) && IsNumeric(right) && left.Length == right.Length)
                {
                    // It's a range – expand it
                    if (int.TryParse(left, out var lo) && int.TryParse(right, out var hi))
                    {
                        int width = left.Length;
                        for (int z = lo; z <= hi; z++)
                            result.Add(z.ToString().PadLeft(width, '0'));
                        continue;
                    }
                }
            }

            // Fall through: treat as a literal ZIP
            result.Add(s);
        }
        return result;
    }

    private static bool IsNumeric(string s) => s.Length > 0 && s.All(char.IsDigit);

    // ── Category parsing ──────────────────────────────────────────────────

    private static HashSet<string> ParseCategories(string raw)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return result;

        foreach (var cat in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var c = cat.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(c))
                result.Add(c);
        }
        return result;
    }

    // ── Score parsing ─────────────────────────────────────────────────────

    private static double? ParseScore(string raw)
    {
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var score))
            return score;
        return null; // "no ratings yet" or empty
    }
}
