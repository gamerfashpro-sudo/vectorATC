using System.Globalization;
using System.Windows;
using VectorAtc.Core.Controller;
using VectorAtc.Core.Flight;
using VectorAtc.Core.Settings;
using VectorAtc.Core.Ground;
using VectorAtc.Core.Traffic;
using VectorAtc.Core.Phraseology;
using VectorAtc.Localization;
using VectorAtc.SimLink;
using VectorAtc.ViewModels;

namespace VectorAtc.App;

public partial class VectorApplication : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsStore = new JsonSettingsStore(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VectorATC", "settings.json"));
        var saved = settingsStore.Load();

        // Язык: сохранённый выбор, иначе язык системы (русский или английский).
        var language = saved.Language is "ru" or "en"
            ? saved.Language
            : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
        var localization = new LocalizationService();
        localization.SetCulture(CultureInfo.GetCultureInfo(language));
        var style = Enum.TryParse<PhraseStyle>(saved.Style, out var parsedStyle) && Enum.IsDefined(parsedStyle)
            ? parsedStyle
            : PhraseStyle.Icao;

        var log = new ExchangeLog();
        var engine = new ControllerEngine(
            new PhraseBook(), MockScenarios.TestAirport, style, localization.Culture, log);

        // Этап 1d: демонстрационный источник данных. Настоящий SimConnect подключается на этапе 2.
        var sim = new MockSimDataSource(MockScenarios.ParkedToHoldShort());
        var viewModel = new MainViewModel(
            localization, engine, log, sim, () => sim.Advance(), () => new SimConnectDataSource(), new CabinAnnouncer(), new VectorAtc.App.Speech.WindowsSpeechOutput(),
            new VectorAtc.App.Speech.WindowsSpeechInput(),
            new SimulatedGroundServices(),
            new SimulatedTraffic(MockScenarios.TestAirport.HoldPoint),
            settings: settingsStore,
            simBrief: new VectorAtc.Core.FlightPlan.HttpSimBriefClient());

        new MainWindow { DataContext = viewModel }.Show();
    }
}
