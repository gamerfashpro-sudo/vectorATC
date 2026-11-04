using VectorAtc.Core.Parsing;
using Xunit;

namespace VectorAtc.Tests;

public class RequestParserTests
{
    private static readonly RequestParser Parser = new();

    [Fact]
    public void Typed_callsign_and_taxi_request()
    {
        var r = Parser.Parse("N172SP request taxi");
        Assert.Equal("N172SP", r.Callsign);
        Assert.Equal(IntentType.RequestTaxi, r.Intent);
    }

    [Fact]
    public void Spoken_english_callsign_is_assembled()
    {
        var r = Parser.Parse("November one seven two Sierra Papa, request taxi");
        Assert.Equal("N172SP", r.Callsign);
    }

    [Fact]
    public void Spoken_russian_callsign_and_taxi_request()
    {
        var r = Parser.Parse("Новембер один семь два Сьерра Папа, запрашиваю руление");
        Assert.Equal("N172SP", r.Callsign);
        Assert.Equal(IntentType.RequestTaxi, r.Intent);
    }

    [Fact]
    public void Aviation_digit_variants_tree_fife_niner()
    {
        var r = Parser.Parse("november tree fife niner alpha radio check");
        Assert.Equal("N359A", r.Callsign);
        Assert.Equal(IntentType.RadioCheck, r.Intent);
    }

    [Fact]
    public void Runway_digits_are_not_taken_as_callsign()
    {
        var r = Parser.Parse("N172SP request taxi runway two seven");
        Assert.Equal("N172SP", r.Callsign);
    }

    [Fact]
    public void Digits_without_letters_are_not_a_callsign()
    {
        var r = Parser.Parse("request taxi runway two seven");
        Assert.Null(r.Callsign);
        Assert.Equal(IntentType.RequestTaxi, r.Intent);
    }

    [Fact]
    public void Russian_radio_check()
    {
        var r = Parser.Parse("UAL123 проверка связи");
        Assert.Equal("UAL123", r.Callsign);
        Assert.Equal(IntentType.RadioCheck, r.Intent);
    }

    [Fact]
    public void Pushback_beats_taxi_when_both_are_said()
    {
        var r = Parser.Parse("UAL123 request pushback and taxi");
        Assert.Equal(IntentType.RequestPushback, r.Intent);
    }

    [Fact]
    public void Say_again_in_both_languages()
    {
        Assert.Equal(IntentType.SayAgain, Parser.Parse("UAL123 say again").Intent);
        Assert.Equal(IntentType.SayAgain, Parser.Parse("UAL123 повторите").Intent);
    }

    [Fact]
    public void Ready_for_departure_in_both_languages()
    {
        Assert.Equal(IntentType.ReadyForDeparture, Parser.Parse("UAL123 ready for departure").Intent);
        Assert.Equal(IntentType.ReadyForDeparture, Parser.Parse("UAL123 готов к взлёту").Intent);
    }

    [Fact]
    public void Emergency_outranks_everything_else()
    {
        var r = Parser.Parse("Mayday mayday UAL123 request taxi");
        Assert.Equal(IntentType.Emergency, r.Intent);
        Assert.Equal("UAL123", r.Callsign);
    }

    [Fact]
    public void Unrecognised_text_is_unknown_without_callsign()
    {
        var r = Parser.Parse("hello there");
        Assert.Equal(IntentType.Unknown, r.Intent);
        Assert.Null(r.Callsign);
    }

    [Fact]
    public void Raw_text_is_preserved()
    {
        Assert.Equal("N172SP, Request Taxi!", Parser.Parse("N172SP, Request Taxi!").RawText);
    }
}
