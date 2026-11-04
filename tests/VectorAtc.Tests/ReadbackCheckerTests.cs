using VectorAtc.Core.Controller;
using VectorAtc.Core.Models;
using Xunit;

namespace VectorAtc.Tests;

public class ReadbackCheckerTests
{
    private static readonly AirportInfo Airport = new(
        "TEST", "27", new[] { "A", "B" }, new GeoPoint(55.0, 37.0), 25, 118_100);

    private static string Errors(string text) =>
        string.Join(",", ReadbackChecker.Check(text, "N172SP", Airport).Errors);

    [Fact]
    public void Correct_readback_in_words_has_no_errors()
    {
        Assert.Equal("", Errors("taxi to holding point runway two seven via alpha bravo November one seven two Sierra Papa"));
    }

    [Fact]
    public void Correct_readback_with_typed_numbers_and_callsign()
    {
        Assert.Equal("", Errors("Runway 27, taxi via A B, N172SP"));
    }

    [Fact]
    public void Correct_russian_readback()
    {
        Assert.Equal("", Errors("рулю на предварительный полосы два семь по РД альфа браво N172SP"));
    }

    [Fact]
    public void Wrong_runway_is_reported()
    {
        Assert.Equal("Runway", Errors("runway three six via alpha bravo N172SP"));
    }

    [Fact]
    public void Missing_callsign_is_reported()
    {
        Assert.Equal("Callsign", Errors("runway two seven via alpha bravo"));
    }

    [Fact]
    public void Wrong_taxiway_order_is_reported()
    {
        Assert.Equal("Route", Errors("runway two seven via bravo alpha N172SP"));
    }

    [Fact]
    public void Missing_via_is_a_route_error()
    {
        Assert.Equal("Route", Errors("runway two seven N172SP"));
    }

    [Fact]
    public void Empty_readback_reports_everything()
    {
        Assert.Equal("Callsign,Runway,Route", Errors("roger"));
    }
}
