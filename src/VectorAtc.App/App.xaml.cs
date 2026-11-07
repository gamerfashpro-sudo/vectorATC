using System.Globalization;
using System.Windows;
using VectorAtc.Core.Controller;
using VectorAtc.Core.Flight;
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

        var localization = new LocalizationService();
        if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru")
            localization.SetCulture(CultureInfo.GetCultureInfo("ru"));

        var log = new ExchangeLog();
        var engine = new ControllerEngine(
            new PhraseBook(), MockScenarios.TestAirport, PhraseStyle.Icao, localization.Culture, log);

        // Этап 1d: демонстрационный источник данных. Настоящий SimConnect подключается на этапе 2.
        var sim = new MockSimDataSource(MockScenarios.ParkedToHoldShort());
        var viewModel = new MainViewModel(
            localization, engine, log, sim, () => sim.Advance(), () => new SimConnectDataSource(), new CabinAnnouncer(), new VectorAtc.App.Speech.WindowsSpeechOutput());

        new MainWindow { DataContext = viewModel }.Show();
    }
}
