using System.Net;
using VectorAtc.Core.Controller;
using VectorAtc.Core.FlightPlan;
using VectorAtc.Core.Models;
using VectorAtc.Core.Phraseology;
using VectorAtc.Core.Settings;
using VectorAtc.Localization;
using VectorAtc.SimLink;
using VectorAtc.ViewModels;
using Xunit;

namespace VectorAtc.Tests;

/// <summary>
/// Образцы JSON ниже собраны по именам полей из описаний сторонних разработчиков, а не из настоящего ответа SimBrief:
/// его в среде разработки получить не удалось. Тесты доказывают, что разбор делает то, что задумано, но не то, что SimBrief
/// отвечает именно так.
/// </summary>
public class SimBriefTests
{
    private const string Sample = """
    {
      "fetch": { "status": "Success" },
      "general": { "icao_airline": "AFL", "flight_number": "1234", "route": "UNOKO L851 ABC" },
      "atc": { "callsign": "AFL1234" },
      "origin": { "icao_code": "UUEE", "plan_rwy": "24C" },
      "destination": { "icao_code": "ULLI", "plan_rwy": "10R" },
      "aircraft": { "icaocode": "A320", "reg": "VQ-BAA" },
      "weights": { "pax_count": "142" }
    }
    """;

    [Fact]
    public void Parses_the_fields_the_app_needs()
    {
        var r = SimBriefParser.Parse(Sample);
        Assert.True(r.IsSuccess);
        var p = r.Plan!;
        Assert.Equal("AFL1234", p.Callsign);
        Assert.Equal("AFL", p.AirlineIcao);
        Assert.Equal("1234", p.FlightNumber);
        Assert.Equal("UUEE", p.OriginIcao);
        Assert.Equal("24C", p.OriginRunway);
        Assert.Equal("ULLI", p.DestinationIcao);
        Assert.Equal("10R", p.DestinationRunway);
        Assert.Equal("A320", p.AircraftIcao);
        Assert.Equal("VQ-BAA", p.Registration);
        Assert.Equal("UNOKO L851 ABC", p.Route);
        Assert.Equal(142, p.Passengers);
    }

    [Fact]
    public void Empty_object_values_and_numbers_are_tolerated()
    {
        var json = """
        { "general": { "icao_airline": {}, "flight_number": 77 },
          "atc": { "callsign": {} },
          "origin": { "icao_code": "EGLL", "plan_rwy": {} },
          "destination": { "icao_code": "EDDF" },
          "weights": { "pax_count": 150 } }
        """;
        var r = SimBriefParser.Parse(json);
        Assert.True(r.IsSuccess);
        Assert.Equal("", r.Plan!.Callsign);
        Assert.Equal("", r.Plan.AirlineIcao);
        Assert.Equal("77", r.Plan.FlightNumber);
        Assert.Equal("", r.Plan.OriginRunway);
        Assert.Equal("", r.Plan.DestinationRunway);
        Assert.Equal(150, r.Plan.Passengers);
    }

    [Fact]
    public void Non_numeric_passenger_count_is_unknown()
    {
        var r = SimBriefParser.Parse("""{ "origin": {"icao_code":"A"}, "weights": {"pax_count":"many"} }""");
        Assert.True(r.IsSuccess);
        Assert.Null(r.Plan!.Passengers);
    }

    [Fact]
    public void Error_status_means_not_found()
    {
        var r = SimBriefParser.Parse("""{ "fetch": { "status": "Error: Unknown UserID" } }""");
        Assert.False(r.IsSuccess);
        Assert.Equal(FlightPlanError.NotFound, r.Error);
    }

    [Fact]
    public void Garbage_or_planless_json_is_a_bad_response()
    {
        Assert.Equal(FlightPlanError.BadResponse, SimBriefParser.Parse("<html>").Error);
        Assert.Equal(FlightPlanError.BadResponse, SimBriefParser.Parse("[]").Error);
        Assert.Equal(FlightPlanError.BadResponse, SimBriefParser.Parse("{}").Error);
        Assert.Equal(FlightPlanError.BadResponse, SimBriefParser.Parse("").Error);
    }

    [Fact]
    public void Url_uses_userid_for_digits_and_username_otherwise()
    {
        var byId = HttpSimBriefClient.BuildUrl(" 123456 ");
        Assert.Equal("https://www.simbrief.com/api/xml.fetcher.php?userid=123456&client=VectorATC&json=v2", byId);

        var byName = HttpSimBriefClient.BuildUrl("pilot name&x=1");
        Assert.Contains("username=pilot%20name%26x%3D1", byName);
        Assert.DoesNotContain("userid=", byName);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? Respond { get; set; }
        public Exception? Throw { get; set; }
        public List<string> Urls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.ToString());
            if (Throw is not null) throw Throw;
            return Task.FromResult(Respond!(request));
        }
    }

    private static HttpSimBriefClient Client(FakeHandler h) => new(new HttpClient(h));

    [Fact]
    public async Task Client_returns_the_parsed_plan_and_requests_the_built_url()
    {
        var h = new FakeHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Sample) } };
        var r = await Client(h).FetchLatestAsync("123456");
        Assert.True(r.IsSuccess);
        Assert.Equal(HttpSimBriefClient.BuildUrl("123456"), Assert.Single(h.Urls));
    }

    [Fact]
    public async Task Client_maps_http_400_to_not_found_and_other_errors_to_network()
    {
        var bad = new FakeHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.BadRequest) };
        Assert.Equal(FlightPlanError.NotFound, (await Client(bad).FetchLatestAsync("x")).Error);

        var down = new FakeHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) };
        Assert.Equal(FlightPlanError.Network, (await Client(down).FetchLatestAsync("x")).Error);

        var offline = new FakeHandler { Throw = new HttpRequestException("no route") };
        Assert.Equal(FlightPlanError.Network, (await Client(offline).FetchLatestAsync("x")).Error);
    }

    [Fact]
    public async Task Client_does_not_call_the_network_for_blank_input()
    {
        var h = new FakeHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) };
        var r = await Client(h).FetchLatestAsync("  ");
        Assert.Equal(FlightPlanError.NotFound, r.Error);
        Assert.Empty(h.Urls);
    }

    [Fact]
    public async Task Client_rethrows_when_the_caller_cancelled()
    {
        var h = new FakeHandler { Throw = new TaskCanceledException() };
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(() => Client(h).FetchLatestAsync("x", cts.Token));
    }

    private sealed class FakeSimBrief : ISimBriefClient
    {
        public FlightPlanResult Result { get; set; } = FlightPlanResult.Fail(FlightPlanError.Network);
        public List<string> Asked { get; } = new();

        public Task<FlightPlanResult> FetchLatestAsync(string idOrUsername, CancellationToken cancellationToken = default)
        {
            Asked.Add(idOrUsername);
            return Task.FromResult(Result);
        }
    }

    private sealed class MemorySettings : ISettingsStore
    {
        public UserSettings Stored { get; set; } = new();
        public UserSettings Load() => Stored;
        public bool Save(UserSettings s) { Stored = s; return true; }
    }

    private static (MainViewModel Vm, FakeSimBrief Client, MemorySettings Store) Fixture(UserSettings? initial = null)
    {
        var loc = new LocalizationService();
        var log = new ExchangeLog();
        var store = new MemorySettings { Stored = initial ?? new UserSettings() };
        var client = new FakeSimBrief { Result = SimBriefParser.Parse(Sample) };
        var engine = new ControllerEngine(new PhraseBook(), MockScenarios.TestAirport, PhraseStyle.Icao, loc.Culture, log);
        var vm = new MainViewModel(loc, engine, log, new MockSimDataSource(), null, null, null, null, null, null, null, null, store, client);
        return (vm, client, store);
    }

    [Fact]
    public async Task Loading_a_plan_sets_callsign_summary_and_status()
    {
        var (vm, client, _) = Fixture();
        vm.SimBriefId = "123456";

        await vm.LoadFlightPlanAsync();

        Assert.Equal("123456", Assert.Single(client.Asked));
        Assert.Equal("AFL1234", vm.Callsign);
        Assert.Equal("Plan loaded", vm.PlanStatus);
        Assert.Contains("AFL1234 · A320 (VQ-BAA)", vm.PlanSummary);
        Assert.Contains("UUEE 24C → ULLI 10R", vm.PlanSummary);
        Assert.Contains("PAX 142", vm.PlanSummary);
    }

    [Fact]
    public async Task Failed_load_keeps_the_callsign_and_reports_the_reason()
    {
        var (vm, client, _) = Fixture();
        client.Result = FlightPlanResult.Fail(FlightPlanError.NotFound);
        vm.Callsign = "KEEP1";
        vm.SimBriefId = "nobody";

        await vm.LoadFlightPlanAsync();

        Assert.Equal("KEEP1", vm.Callsign);
        Assert.Equal("", vm.PlanSummary);
        Assert.Contains("not found", vm.PlanStatus);

        client.Result = FlightPlanResult.Fail(FlightPlanError.Network);
        await vm.LoadFlightPlanAsync();
        Assert.Contains("No connection", vm.PlanStatus);
    }

    [Fact]
    public async Task Blank_id_does_not_call_the_client_and_the_command_is_disabled()
    {
        var (vm, client, _) = Fixture();
        Assert.False(vm.LoadPlanCommand.CanExecute(null));
        await vm.LoadFlightPlanAsync();
        Assert.Empty(client.Asked);

        vm.SimBriefId = "42";
        Assert.True(vm.LoadPlanCommand.CanExecute(null));
    }

    [Fact]
    public void Id_is_saved_and_restored_without_writing_back()
    {
        var (vm, _, store) = Fixture();
        vm.SimBriefId = "  user_1 ";
        Assert.Equal("user_1", store.Stored.SimBriefId);

        var (restored, _, store2) = Fixture(new UserSettings(SimBriefId: "777"));
        Assert.Equal("777", restored.SimBriefId);
        Assert.Null(store2.Stored.Callsign);
    }

    [Fact]
    public async Task Status_follows_the_interface_language()
    {
        var (vm, _, _) = Fixture();
        vm.SimBriefId = "1";
        await vm.LoadFlightPlanAsync();
        Assert.Equal("Plan loaded", vm.PlanStatus);

        vm.ToggleLanguageCommand.Execute(null);
        Assert.Equal("План загружен", vm.PlanStatus);
    }

    [Fact]
    public void Plan_card_is_hidden_without_a_client()
    {
        var loc = new LocalizationService();
        var log = new ExchangeLog();
        var engine = new ControllerEngine(new PhraseBook(), MockScenarios.TestAirport, PhraseStyle.Icao, loc.Culture, log);
        var vm = new MainViewModel(loc, engine, log, new MockSimDataSource());
        Assert.False(vm.IsPlanVisible);
    }
}
