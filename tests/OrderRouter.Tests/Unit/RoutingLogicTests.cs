using FluentAssertions;
using OrderRouter.Api.Models;
using OrderRouter.Api.Services;
using OrderRouter.Tests.Infrastructure;
using Xunit;

namespace OrderRouter.Tests.Unit;

/// <summary>
/// Contains unit tests for verifying the routing logic used to assign suppliers to order requests based on coverage,
/// quality, and fulfillment mode preferences.
/// </summary>
/// <remarks>These tests cover scenarios such as supplier consolidation, preference for higher-rated or local
/// suppliers, mail-order eligibility, handling of unknown products, and routing feasibility. The class is intended to
/// ensure that the routing engine selects suppliers according to business rules and expected behaviors.</remarks>
public class RoutingLogicTests
{
    // ── helpers ───────────────────────────────────────────────────────────

    private static RoutingEngine Engine(IDataLoader loader) => new(loader);

    // Zip served by SUP-LOCAL only
    private const string LocalZip = "00001";
    // Zip served by nobody locally
    private const string NowhereZip = "99999";

    private static StubDataLoader MakeLoader(params Supplier[] suppliers)
    {
        var loader = new StubDataLoader(suppliers);
        loader.AddProduct("WC-001", "wheelchair");
        loader.AddProduct("OX-001", "oxygen");
        loader.AddProduct("CP-001", "cpap");
        return loader;
    }

    // ── Single-supplier consolidation ─────────────────────────────────────

    [Fact]
    public void SingleSupplier_CoversAllCategories_ConsolidatesIntoOne()
    {
        var loader = MakeLoader(
            Sup("SUP-A", zips: [LocalZip], cats: ["wheelchair", "oxygen"], score: 9, mail: false));

        var result = Engine(loader).Route(new OrderRequest
        {
            OrderId = "T1",
            CustomerZip = LocalZip,
            Items = [new() { ProductCode = "WC-001", Quantity = 1 },
                     new() { ProductCode = "OX-001", Quantity = 1 }]
        });

        result.Feasible.Should().BeTrue();
        result.Routing.Should().HaveCount(1);
        result.Routing![0].SupplierId.Should().Be("SUP-A");
        result.Routing[0].Items.Should().HaveCount(2);
    }

    // ── Multi-supplier split ──────────────────────────────────────────────

    [Fact]
    public void NoSingleCoverAll_SplitsAcrossSpecialists()
    {
        var loader = MakeLoader(
            Sup("SUP-WC", zips: [LocalZip], cats: ["wheelchair"], score: 8, mail: false),
            Sup("SUP-OX", zips: [LocalZip], cats: ["oxygen"], score: 7, mail: false));

        var result = Engine(loader).Route(new OrderRequest
        {
            OrderId = "T2",
            CustomerZip = LocalZip,
            Items = [new() { ProductCode = "WC-001", Quantity = 1 },
                     new() { ProductCode = "OX-001", Quantity = 1 }]
        });

        result.Feasible.Should().BeTrue();
        result.Routing.Should().HaveCount(2);
    }

    // ── Quality preference ────────────────────────────────────────────────

    [Fact]
    public void HigherScore_PreferredOverLower_WhenBothLocal()
    {
        var loader = MakeLoader(
            Sup("SUP-LOW", zips: [LocalZip], cats: ["wheelchair"], score: 4, mail: false),
            Sup("SUP-HIGH", zips: [LocalZip], cats: ["wheelchair"], score: 9, mail: false));

        var result = Engine(loader).Route(new OrderRequest
        {
            OrderId = "T3",
            CustomerZip = LocalZip,
            Items = [new() { ProductCode = "WC-001", Quantity = 1 }]
        });

        result.Feasible.Should().BeTrue();
        result.Routing![0].SupplierId.Should().Be("SUP-HIGH");
    }

    // ── Geo preference: local over mail-order ─────────────────────────────

    [Fact]
    public void LocalSupplier_PreferredOver_MailOrder_WhenSameScore()
    {
        const string mailOnlyZip = "00002";
        var loader = MakeLoader(
            Sup("SUP-MAIL", zips: [mailOnlyZip], cats: ["wheelchair"], score: 9, mail: true),
            Sup("SUP-LOCAL", zips: [LocalZip], cats: ["wheelchair"], score: 9, mail: false));

        // mail_order: true so mail supplier is eligible, but local should win
        var result = Engine(loader).Route(new OrderRequest
        {
            OrderId = "T4",
            CustomerZip = LocalZip,
            MailOrder = true,
            Items = [new() { ProductCode = "WC-001", Quantity = 1 }]
        });

        result.Feasible.Should().BeTrue();
        result.Routing![0].SupplierId.Should().Be("SUP-LOCAL");
    }

    [Fact]
    public void LocalSupplier_PreferredOver_MailOrder_WhenRatingsAreWithinThreshold()
    {
        var loader = MakeLoader(
            Sup("SUP-MAIL", zips: ["00002"], cats: ["wheelchair"], score: 9.1, mail: true),
            Sup("SUP-LOCAL", zips: [LocalZip], cats: ["wheelchair"], score: 9.0, mail: false));

        // mail_order: true makes both eligible. 
        // 9.1 and 9.0 are "similar," so SUP-LOCAL should win based on geography.
        var result = Engine(loader).Route(new OrderRequest
        {
            OrderId = "T11",
            CustomerZip = LocalZip,
            MailOrder = true,
            Items = [new() { ProductCode = "WC-001", Quantity = 1 }]
        });

        result.Feasible.Should().BeTrue();
        result.Routing.Should().HaveCount(1);
        result.Routing![0].SupplierId.Should().Be("SUP-LOCAL");
    }


    // ── Mail-order eligibility ─────────────────────────────────────────────

    [Fact]
    public void MailOrder_False_ExcludesNonLocalSupplier()
    {
        var loader = MakeLoader(
            Sup("SUP-MAIL", zips: ["00002"], cats: ["wheelchair"], score: 10, mail: true));

        var result = Engine(loader).Route(new OrderRequest
        {
            OrderId = "T5",
            CustomerZip = LocalZip,
            MailOrder = false,
            Items = [new() { ProductCode = "WC-001", Quantity = 1 }]
        });

        result.Feasible.Should().BeFalse();
    }

    [Fact]
    public void MailOrder_True_IncludesMailOnlySupplier()
    {
        var loader = MakeLoader(
            Sup("SUP-MAIL", zips: ["00002"], cats: ["wheelchair"], score: 8, mail: true));

        var result = Engine(loader).Route(new OrderRequest
        {
            OrderId = "T6",
            CustomerZip = LocalZip,
            MailOrder = true,
            Items = [new() { ProductCode = "WC-001", Quantity = 1 }]
        });

        result.Feasible.Should().BeTrue();
        result.Routing![0].Items[0].FulfillmentMode.Should().Be("mail_order");
    }

    // ── Unrated suppliers ─────────────────────────────────────────────────

    [Fact]
    public void Rated_PreferredOver_Unrated()
    {
        var loader = MakeLoader(
            Sup("SUP-UNRATED", zips: [LocalZip], cats: ["wheelchair"], score: null, mail: false),
            Sup("SUP-RATED", zips: [LocalZip], cats: ["wheelchair"], score: 1, mail: false));

        var result = Engine(loader).Route(new OrderRequest
        {
            OrderId = "T7",
            CustomerZip = LocalZip,
            Items = [new() { ProductCode = "WC-001", Quantity = 1 }]
        });

        result.Routing![0].SupplierId.Should().Be("SUP-RATED");
    }

    // ── Unknown product code ──────────────────────────────────────────────

    [Fact]
    public void UnknownProductCode_ReturnsFeasibleFalse()
    {
        var result = Engine(new StubDataLoader()).Route(new OrderRequest
        {
            OrderId = "T8",
            CustomerZip = LocalZip,
            Items = [new() { ProductCode = "BOGUS-999", Quantity = 1 }]
        });

        result.Feasible.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("BOGUS-999"));
    }

    // ── No coverage anywhere ──────────────────────────────────────────────

    [Fact]
    public void NoCoverage_ReturnsFeasibleFalse()
    {
        // Supplier serves a different ZIP and can't mail-order
        var loader = MakeLoader(
            Sup("SUP-FAR", zips: ["99998"], cats: ["wheelchair"], score: 9, mail: false));

        var result = Engine(loader).Route(new OrderRequest
        {
            OrderId = "T9",
            CustomerZip = NowhereZip,
            Items = [new() { ProductCode = "WC-001", Quantity = 1 }]
        });

        result.Feasible.Should().BeFalse();
    }

    // ── Consolidation: generalist beats two specialists ───────────────────

    [Fact]
    public void Generalist_CoversAll_PreferredOver_TwoSpecialists()
    {
        var loader = MakeLoader(
            Sup("SUP-GEN", zips: [LocalZip], cats: ["wheelchair", "oxygen"], score: 7, mail: false),
            Sup("SUP-WC", zips: [LocalZip], cats: ["wheelchair"], score: 9, mail: false),
            Sup("SUP-OX", zips: [LocalZip], cats: ["oxygen"], score: 9, mail: false));

        var result = Engine(loader).Route(new OrderRequest
        {
            OrderId = "T10",
            CustomerZip = LocalZip,
            Items = [new() { ProductCode = "WC-001", Quantity = 1 },
                     new() { ProductCode = "OX-001", Quantity = 1 }]
        });

        // Single-supplier path should be chosen (fewer shipments)
        result.Feasible.Should().BeTrue();
        result.Routing.Should().HaveCount(1);
    }

    // ── Builder helpers ───────────────────────────────────────────────────

    private static Supplier Sup(string id, string[] zips, string[] cats, double? score, bool mail) =>
        new()
        {
            SupplierId = id,
            SupplierName = id,
            ServiceZips = [.. zips],
            ProductCategories = cats.ToHashSet(StringComparer.OrdinalIgnoreCase),
            SatisfactionScore = score,
            CanMailOrder = mail,
        };
}

