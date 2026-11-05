using VectorAtc.Core.Models;

namespace VectorAtc.Core.Flight;

public enum CabinAnnouncement
{
    SafetyBriefing,
    PrepareForTakeoff,
    CruiseInfo,
    DescentPreparation,
    LandingPreparation,
    Landed,
    Deplane
}

/// <summary>
/// Выбирает объявления бортпроводников по смене фазы полёта. Каждое объявление звучит один раз за рейс.
/// Границу подготовки к посадке задаёт абсолютная высота (<see cref="LandingPreparationAltitudeFeet"/>): допущение,
/// потому что высоты над землёй в данных нет.
/// </summary>
public sealed class CabinAnnouncer
{
    private readonly FlightPhaseDetector _detector;
    private bool _briefed, _departed, _cruised, _descentCalled, _landingCalled, _landedCalled;

    public CabinAnnouncer(FlightPhaseDetector? detector = null) => _detector = detector ?? new FlightPhaseDetector();

    public double LandingPreparationAltitudeFeet { get; init; } = 4000;

    public FlightPhase Phase => _detector.Phase;

    public IReadOnlyList<CabinAnnouncement> Update(AircraftState state)
    {
        var before = _detector.Phase;
        var after = _detector.Update(state);
        var result = new List<CabinAnnouncement>();

        if (after == FlightPhase.Taxi && !_briefed && !_departed)
        {
            _briefed = true;
            result.Add(CabinAnnouncement.SafetyBriefing);
        }

        if (after == FlightPhase.TakeoffRoll && before != FlightPhase.TakeoffRoll && !_departed)
        {
            _departed = true;
            result.Add(CabinAnnouncement.PrepareForTakeoff);
        }

        if (after == FlightPhase.Cruise && !_cruised && _departed)
        {
            _cruised = true;
            result.Add(CabinAnnouncement.CruiseInfo);
        }

        if (after == FlightPhase.Descent && _departed)
        {
            if (!_descentCalled)
            {
                _descentCalled = true;
                result.Add(CabinAnnouncement.DescentPreparation);
            }

            if (!_landingCalled && state.AltitudeFeet <= LandingPreparationAltitudeFeet)
            {
                _landingCalled = true;
                result.Add(CabinAnnouncement.LandingPreparation);
            }
        }

        if (before == FlightPhase.LandingRoll && after is FlightPhase.Taxi or FlightPhase.Parked && _departed && !_landedCalled)
        {
            _landedCalled = true;
            result.Add(CabinAnnouncement.Landed);
        }

        if (after == FlightPhase.Parked && _landedCalled)
        {
            result.Add(CabinAnnouncement.Deplane);
            _briefed = _departed = _cruised = _descentCalled = _landingCalled = _landedCalled = false;
        }

        return result;
    }
}
