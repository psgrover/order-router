using FluentAssertions;
using OrderRouter.Api.Models;
using OrderRouter.Api.Services;
using Xunit;

namespace OrderRouter.Tests.Integration;

/// <summary>
/// Sample Order Integration Tests - these are intended to be run against the real CsvDataLoader and real data files 
/// to verify that the overall system can produce expected results for representative orders. 
/// They are not meant to be exhaustive, but rather to serve as sanity checks and examples of how the routing engine handles realistic scenarios. 
/// Each test corresponds to a specific sample order with known characteristics and expected outcomes based on the underlying data.
/// </summary>
public class SampleOrderIntegrationTests
{
    private static RoutingEngine BuildRealEngine()
    {
        // Walk up from test bin to repo root, then into the src data folder
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        DirectoryInfo? repoRoot = here;
        while (repoRoot != null && !File.Exists(Path.Combine(repoRoot.FullName, "docker-compose.yml")))
            repoRoot = repoRoot.Parent;

        var dataDir = repoRoot != null
            ? Path.Combine(repoRoot.FullName, "src", "OrderRouter.Api", "Data")
            : Path.Combine(AppContext.BaseDirectory, "Data");

        var loader = new CsvDataLoader(dataDir);
        return new RoutingEngine(loader);
    }

    private static readonly Lazy<IRoutingEngine> Engine = new(BuildRealEngine);

    // ── ORD-001: wheelchair + oxygen, NYC zip 10015 ───────────────────────

    [Fact]
    public void ORD001_NYC_WheelchairPlusOxygen_IsFeasible()
    {
        var result = Engine.Value.Route(new OrderRequest
        {
            OrderId = "ORD-001",
            CustomerZip = "10015",
            MailOrder = false,
            Items =
            [
                new() { ProductCode = "WC-STD-001", Quantity = 1 },
                new() { ProductCode = "OX-PORT-024", Quantity = 1 },
            ]
        });

        result.Feasible.Should().BeTrue();
        result.Routing.Should().NotBeNullOrEmpty();
        // All items must be accounted for
        var allItems = result.Routing!.SelectMany(r => r.Items).ToList();
        allItems.Should().HaveCount(2);
        allItems.Select(i => i.FulfillmentMode).Should().AllBe("local");
    }

    // ── ORD-002: rush, Houston 77059, 4 categories ────────────────────────

    [Fact]
    public void ORD002_Houston_MultiCategory_IsFeasible()
    {
        var result = Engine.Value.Route(new OrderRequest
        {
            OrderId = "ORD-002",
            CustomerZip = "77059",
            MailOrder = false,
            Priority = "rush",
            Items =
            [
                new() { ProductCode = "HB-FUL-018",  Quantity = 1 },
                new() { ProductCode = "PL-ELEC-043",  Quantity = 1 },
                new() { ProductCode = "CM-BED-048",  Quantity = 1 },
                new() { ProductCode = "BP-AUTO-077",  Quantity = 1 },
            ]
        });

        result.Feasible.Should().BeTrue();
        var allItems = result.Routing!.SelectMany(r => r.Items).ToList();
        allItems.Should().HaveCount(4);
    }

    // ── ORD-003: mail order, Boston 02130, respiratory ────────────────────

    [Fact]
    public void ORD003_MailOrder_Respiratory_IsFeasible()
    {
        var result = Engine.Value.Route(new OrderRequest
        {
            OrderId = "ORD-003",
            CustomerZip = "02130",
            MailOrder = true,
            Items =
            [
                new() { ProductCode = "CP-STD-031",    Quantity = 1 },
                new() { ProductCode = "CP-MSK-FF-035", Quantity = 2 },
                new() { ProductCode = "NB-COMP-039",   Quantity = 1 },
            ]
        });

        result.Feasible.Should().BeTrue();
        result.Routing!.SelectMany(r => r.Items).Should().HaveCount(3);
    }
}
