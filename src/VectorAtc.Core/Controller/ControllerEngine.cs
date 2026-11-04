using System.Globalization;
using VectorAtc.Core.Models;
using VectorAtc.Core.Parsing;
using VectorAtc.Core.Phraseology;

namespace VectorAtc.Core.Controller;

public enum FlightState
{
    Parked,
    PushbackApproved,
    TaxiClearanceIssued,
    Taxiing,
    TowerContacted,
    LinedUp
}

public enum ControllerPhase
{
    Ground,
    Tower
}

/// <summary>
/// Конечный автомат диспетчера для одного вылетающего самолёта игрока (Ground → Tower).
/// Ответы строятся только из шаблонов <see cref="IPhraseBook"/>. Все реплики попадают в <see cref="ExchangeLog"/>.
/// Сессия привязывается к позывному из первого сообщения; реплики с другим позывным получают «say again».
/// </summary>
public sealed class ControllerEngine
{
    private readonly IPhraseBook _phrases;
    private readonly AirportInfo _airport;
    private readonly PhraseStyle _style;
    private readonly CultureInfo _culture;
    private readonly ExchangeLog _log;
    private readonly RequestParser _parser = new();
    private IReadOnlyList<string> _lastReply = Array.Empty<string>();

    public ControllerEngine(IPhraseBook phrases, AirportInfo airport, PhraseStyle style, CultureInfo culture, ExchangeLog log)
    {
        _phrases = phrases ?? throw new ArgumentNullException(nameof(phrases));
        _airport = airport ?? throw new ArgumentNullException(nameof(airport));
        _style = style;
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public FlightState State { get; private set; } = FlightState.Parked;

    public string? Callsign { get; private set; }

    public ControllerPhase Phase => State >= FlightState.TowerContacted ? ControllerPhase.Tower : ControllerPhase.Ground;

    /// <summary>Обрабатывает реплику пилота и возвращает передачи диспетчера (может быть пусто).</summary>
    public IReadOnlyList<string> HandlePilotMessage(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _log.Add(LogSource.Pilot, text);
        var request = _parser.Parse(text);

        if (Callsign is null)
        {
            if (request.Callsign is null) return Reply(Say(PhraseId.UnknownStation));
            Callsign = request.Callsign;
        }
        else if (request.Callsign is not null && !ReadbackChecker.Mentions(text, Callsign))
        {
            return Reply(Say(PhraseId.UnknownStation));
        }

        if (request.Intent == IntentType.Emergency)
            return Reply(Say(PhraseId.EmergencyAcknowledge));

        // Повторение клиренса может содержать слово «taxi», поэтому оно проверяется раньше классификации намерения.
        if (State == FlightState.TaxiClearanceIssued
            && request.Intent is not (IntentType.SayAgain or IntentType.RadioCheck))
            return HandleReadback(text);

        switch (request.Intent)
        {
            case IntentType.RadioCheck:
                return Reply(Say(PhraseId.RadioCheckReply));

            case IntentType.SayAgain:
                return Repeat();

            case IntentType.RequestPushback when State is FlightState.Parked or FlightState.PushbackApproved:
                State = FlightState.PushbackApproved;
                return Reply(Say(PhraseId.PushbackApproved));

            case IntentType.RequestTaxi when State is FlightState.Parked or FlightState.PushbackApproved:
                State = FlightState.TaxiClearanceIssued;
                return Reply(Say(PhraseId.TaxiClearance));

            case IntentType.ReadyForDeparture when State == FlightState.TowerContacted:
                State = FlightState.LinedUp;
                return Reply(Say(PhraseId.LineUpAndWait));

            case IntentType.Unknown:
                return Reply(Say(PhraseId.SayAgainRequest));

            default:
                return Reply(Say(PhraseId.Unable));
        }
    }

    /// <summary>
    /// Следит за положением самолёта: остановка у точки ожидания на руление завершает его и передаёт на Tower.
    /// </summary>
    public IReadOnlyList<string> OnAircraftState(AircraftState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (State != FlightState.Taxiing || !state.OnGround || state.GroundSpeedKnots >= 1)
            return Array.Empty<string>();
        if (Geo.Geo.DistanceMeters(state.Position, _airport.HoldPoint) > _airport.HoldRadiusMeters)
            return Array.Empty<string>();

        State = FlightState.TowerContacted;
        return Reply(Say(PhraseId.ContactTower));
    }

    private IReadOnlyList<string> HandleReadback(string text)
    {
        var result = ReadbackChecker.Check(text, Callsign!, _airport);
        if (result.IsCorrect)
        {
            State = FlightState.Taxiing;
            return Reply(Say(PhraseId.ReadbackCorrect));
        }

        foreach (var error in result.Errors)
            _log.Add(LogSource.Violation, $"Readback error: {error}");

        // Клиренс повторяется, состояние остаётся прежним: пилот должен повторить его верно.
        return Reply(Say(PhraseId.ReadbackIncorrect), Say(PhraseId.TaxiClearance));
    }

    private IReadOnlyList<string> Repeat()
    {
        foreach (var line in _lastReply)
            _log.Add(LogSource.Controller, line);
        return _lastReply;
    }

    private IReadOnlyList<string> Reply(params string[] lines)
    {
        _lastReply = lines;
        return lines;
    }

    private string Say(PhraseId id)
    {
        var route = string.Join(", ", _airport.TaxiRoute.Select(Vocabulary.LetterName));
        var values = new Dictionary<string, string>
        {
            ["callsign"] = Callsign ?? "",
            ["runway"] = _airport.ActiveRunway,
            ["route"] = route,
            ["frequency"] = (_airport.TowerFrequencyKhz / 1000.0).ToString("0.0##", CultureInfo.InvariantCulture),
        };

        var text = _phrases.Render(id, _style, _culture, values);
        _log.Add(LogSource.Controller, text);
        return text;
    }
}
