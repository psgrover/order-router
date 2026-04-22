using FluentAssertions;
using OrderRouter.Api.Models;
using OrderRouter.Api.Services;
using OrderRouter.Tests.Infrastructure;
using Xunit;

namespace OrderRouter.Tests.Unit;

/// <summary>
/// Contains unit tests that verify validation behavior for the routing engine using various order request scenarios.
/// </summary>
/// <remarks>These tests ensure that the routing engine correctly identifies invalid input cases, such as missing
/// items or customer zip codes, and returns appropriate error information. Each test method is decorated with the
/// [Fact] attribute for use with the xUnit testing framework.</remarks>
public class ValidationTests
{
    private static RoutingEngine BuildEngine(IDataLoader loader) =>
        new(loader);

    [Fact]
    public void EmptyItems_ReturnsFeasibleFalse_WithError()
    {
        var engine = BuildEngine(new StubDataLoader());
        var result = engine.Route(new OrderRequest
        {
            OrderId = "X",
            CustomerZip = "10001",
            Items = []
        });

        result.Feasible.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("line item"));
    }

    [Fact]
    public void MissingZip_ReturnsFeasibleFalse_WithError()
    {
        var engine = BuildEngine(new StubDataLoader());
        var result = engine.Route(new OrderRequest
        {
            OrderId = "X",
            CustomerZip = string.Empty,
            Items = [new() { ProductCode = "WC-STD-001", Quantity = 1 }]
        });

        result.Feasible.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("customer_zip"));
    }
}

