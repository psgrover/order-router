using FluentAssertions;
using OrderRouter.Api.Services;
using Xunit;

namespace OrderRouter.Tests.Unit;

/// <summary>
/// Contains unit tests for verifying the parsing and expansion of ZIP code lists and ranges using the
/// CsvDataLoader.ParseZips method.
/// </summary>
/// <remarks>These tests cover scenarios such as explicit ZIP code lists, numeric ranges, mixed formats, and empty
/// input to ensure correct parsing behavior. The class is intended for use with a test framework and is not part of the
/// production codebase.</remarks>
public class ZipParsingTests
{
    [Fact]
    public void ExplicitList_ParsesCorrectly()
    {
        var zips = CsvDataLoader.ParseZips("10001, 10002, 10003");
        zips.Should().BeEquivalentTo(["10001", "10002", "10003"]);
    }

    [Fact]
    public void SimpleRange_ExpandsCorrectly()
    {
        var zips = CsvDataLoader.ParseZips("10001-10005");
        zips.Should().BeEquivalentTo(["10001", "10002", "10003", "10004", "10005"]);
    }

    [Fact]
    public void WideRange_00100To99999_ContainsKnownZips()
    {
        var zips = CsvDataLoader.ParseZips("00100-99999");
        zips.Should().Contain("10015"); // NYC sample order zip
        zips.Should().Contain("77059"); // Houston sample order zip
        zips.Should().HaveCountGreaterThan(90000);
    }

    [Fact]
    public void MixedCommaAndRange_ParsesCorrectly()
    {
        var zips = CsvDataLoader.ParseZips("10451-10453, 10479");
        zips.Should().BeEquivalentTo(["10451", "10452", "10453", "10479"]);
    }

    [Fact]
    public void EmptyString_ReturnsEmptySet()
    {
        CsvDataLoader.ParseZips("").Should().BeEmpty();
    }
}

