using OrderRouter.Api.Models;

namespace OrderRouter.Api.Services;

/// <summary>
/// Implements the multi-supplier routing algorithm.
///
/// Priority (highest → lowest):
///   1. Feasibility  – supplier can carry all item categories needed
///   2. Consolidation – prefer fewer shipments (single supplier when possible)
///   3. Quality       – prefer higher satisfaction score; unrated < rated
///   4. Geography     – prefer local over mail-order when scores are equal
/// </summary>
public interface IRoutingEngine
{
    RouteResponse Route(OrderRequest order);
}

public class RoutingEngine(IDataLoader data) : IRoutingEngine
{
    private readonly IDataLoader _data = data;

    public RouteResponse Route(OrderRequest order)
    {
        var errors = Validate(order);
        if (errors.Count > 0)
            return new RouteResponse { Feasible = false, Errors = errors };

        var resolvedItems = ResolveItems(order);
        if (resolvedItems.Any(r => r.Category == null))
        {
            var unknown = resolvedItems.Where(r => r.Category == null).Select(r => r.ProductCode);
            return new RouteResponse { Feasible = false, Errors = [$"Unknown product code(s): {string.Join(", ", unknown)}"] };
        }

        var eligible = GetEligibleSuppliers(order);

        // Priority 2: Consolidation (Try single supplier first)
        var single = TrySingleSupplier(resolvedItems, eligible);
        if (single != null)
            return new RouteResponse { Feasible = true, Routing = [single] };

        // Priority 1: Feasibility (Fall back to multi-supplier)
        var multi = AssignMultiSupplier(resolvedItems, eligible);
        if (multi == null)
            return new RouteResponse { Feasible = false, Errors = ["No feasible routing found for one or more items."] };

        return new RouteResponse { Feasible = true, Routing = multi };
    }

    private static List<string> Validate(OrderRequest order)
    {
        var errors = new List<string>();
        if (order.Items == null || order.Items.Count == 0) errors.Add("Order must include at least one line item.");
        if (string.IsNullOrWhiteSpace(order.CustomerZip)) errors.Add("Order must include a valid customer_zip.");
        if (string.IsNullOrWhiteSpace(order.OrderId)) errors.Add("Order must include an order_id.");
        return errors;
    }

    private record ResolvedItem(string ProductCode, int Quantity, string? Category);
    private record EligibleSupplier(Supplier Supplier, bool IsLocal);

    private List<ResolvedItem> ResolveItems(OrderRequest order) =>
        [.. order.Items.Select(item => {
            _data.Products.TryGetValue(item.ProductCode, out var product);
            return new ResolvedItem(item.ProductCode, item.Quantity, product?.Category);
        })];

    private List<EligibleSupplier> GetEligibleSuppliers(OrderRequest order)
    {
        var result = new List<EligibleSupplier>();
        foreach (var supplier in _data.Suppliers)
        {
            bool servesLocal = supplier.ServesZip(order.CustomerZip);
            bool servesViaMailOrder = order.MailOrder && supplier.CanMailOrder;

            if (servesLocal) result.Add(new EligibleSupplier(supplier, true));
            else if (servesViaMailOrder) result.Add(new EligibleSupplier(supplier, false));
        }
        return result;
    }

    /// <summary>
    /// Corrected Sorting with Similarity Threshold: 
    /// 1. Has Rating (Rated > Unrated)
    /// 2. Quality Bucket (Similar ratings treated as equal)
    /// 3. Geography (Local > Mail Order)
    /// 4. Fine-grained Quality (Tie-breaker for the tie-breaker)
    /// </summary>
    private static IEnumerable<EligibleSupplier> Ranked(IEnumerable<EligibleSupplier> suppliers) =>
        suppliers.OrderByDescending(s => s.Supplier.SatisfactionScore.HasValue)
                 // Treat ratings within 0.5 of each other as "similar" (buckets of 0.5)
                 .ThenByDescending(s => Math.Floor((s.Supplier.SatisfactionScore ?? 0) * 2))
                 .ThenByDescending(s => s.IsLocal)
                 // Finally, sort by the exact score if everything else is tied
                 .ThenByDescending(s => s.Supplier.SatisfactionScore ?? 0);

    private static SupplierAssignment? TrySingleSupplier(List<ResolvedItem> items, List<EligibleSupplier> eligible)
    {
        var categories = items.Select(i => i.Category!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var es in Ranked(eligible))
        {
            if (categories.All(c => es.Supplier.ProductCategories.Contains(c)))
                return BuildAssignment(es, items);
        }
        return null;
    }

    private static List<SupplierAssignment>? AssignMultiSupplier(List<ResolvedItem> items, List<EligibleSupplier> eligible)
    {
        var categoryGroups = items.GroupBy(i => i.Category!, StringComparer.OrdinalIgnoreCase);
        var supplierToItems = new Dictionary<string, (EligibleSupplier Es, List<ResolvedItem> Items)>();

        foreach (var group in categoryGroups)
        {
            var best = Ranked(eligible).FirstOrDefault(es => es.Supplier.ProductCategories.Contains(group.Key));
            if (best == null) return null;

            if (!supplierToItems.ContainsKey(best.Supplier.SupplierId))
                supplierToItems[best.Supplier.SupplierId] = (best, []);

            supplierToItems[best.Supplier.SupplierId].Items.AddRange(group);
        }

        return [.. supplierToItems.Values.Select(v => BuildAssignment(v.Es, v.Items))];
    }

    private static SupplierAssignment BuildAssignment(EligibleSupplier es, IEnumerable<ResolvedItem> items)
    {
        return new SupplierAssignment
        {
            SupplierId = es.Supplier.SupplierId,
            SupplierName = es.Supplier.SupplierName,
            Items = [.. items.Select(i => new RoutedItem
            {
                ProductCode = i.ProductCode,
                Quantity = i.Quantity,
                Category = i.Category ?? string.Empty,
                FulfillmentMode = es.IsLocal ? "local" : "mail_order"
            })]
        };
    }
}
