namespace OrderRouter.Api.Models;

/// <summary>
/// Order request model representing the incoming JSON payload for routing an order.
/// </summary>
public record OrderRequest
{
    public string OrderId { get; init; } = string.Empty;
    public string CustomerZip { get; init; } = string.Empty;
    public bool MailOrder { get; init; }
    public List<OrderItem> Items { get; init; } = [];
    public string? Priority { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Represents an item in an order, including the product code and quantity ordered.
/// </summary>
public record OrderItem
{
    public string ProductCode { get; init; } = string.Empty;
    public int Quantity { get; init; }
}

/// <summary>
/// Represents the result of a route calculation, including feasibility, routing assignments, and any errors
/// encountered.
/// </summary>
public record RouteResponse
{
    public bool Feasible { get; init; }
    public List<SupplierAssignment>? Routing { get; init; }
    public List<string>? Errors { get; init; }
}

/// <summary>
/// Represents an assignment of items to a supplier, including supplier details and the associated routed items.
/// </summary>
public record SupplierAssignment
{
    public string SupplierId { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public List<RoutedItem> Items { get; init; } = [];
}

/// <summary>
/// Represents an item to be routed, including product information, quantity, category, and fulfillment mode.
/// </summary>
public record RoutedItem
{
    public string ProductCode { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public string Category { get; init; } = string.Empty;
    public string FulfillmentMode { get; init; } = string.Empty;
}
