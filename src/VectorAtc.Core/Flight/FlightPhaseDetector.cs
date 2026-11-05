using VectorAtc.Core.Models;

namespace VectorAtc.Core.Flight;

/// <summary>
/// Определяет фазу полёта по потоку <see cref="AircraftState"/>. Данных «высота над землёй» в состоянии нет, поэтому
/// граница «эшелон / набор» задаётся абсолютной высотой (<see cref="CruiseMinAltitudeFeet"/>): это допущение, а не измерение.
/// Правила: на земле — по скорости; в воздухе — по вертикальной скорости с гистерезисом; уход на второй круг из снижения
/// даёт набор высоты.
/// </summary>
public sealed class FlightPhaseDetector
{
    public const double TakeoffSpeedKnots = 40;
    public const double StoppedSpeedKnots = 1;
    public const double ClimbFpm = 300;
    public const double DescentFpm = -500;

    public double CruiseMinAltitudeFeet { get; init; } = 10000;

    public FlightPhase Phase { get; private set; } = FlightPhase.Parked;

    /// <summary>Обновляет фазу и возвращает её.</summary>
    public FlightPhase Update(AircraftState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Phase = state.OnGround ? OnGround(state) : Airborne(state);
        return Phase;
    }

    private FlightPhase OnGround(AircraftState s)
    {
        var wasInAir = Phase is FlightPhase.Climb or FlightPhase.Cruise or FlightPhase.Descent or FlightPhase.LandingRoll;
        if (s.GroundSpeedKnots < StoppedSpeedKnots) return FlightPhase.Parked;
        if (s.GroundSpeedKnots < TakeoffSpeedKnots) return FlightPhase.Taxi;
        return wasInAir ? FlightPhase.LandingRoll : FlightPhase.TakeoffRoll;
    }

    private FlightPhase Airborne(AircraftState s)
    {
        var vs = s.VerticalSpeedFpm;
        switch (Phase)
        {
            case FlightPhase.Cruise:
                return vs < DescentFpm ? FlightPhase.Descent
                     : vs > ClimbFpm ? FlightPhase.Climb
                     : FlightPhase.Cruise;

            case FlightPhase.Climb:
                if (vs < DescentFpm) return FlightPhase.Descent;
                return Math.Abs(vs) <= ClimbFpm && s.AltitudeFeet >= CruiseMinAltitudeFeet
                    ? FlightPhase.Cruise
                    : FlightPhase.Climb;

            case FlightPhase.Descent:
                return vs > ClimbFpm ? FlightPhase.Climb : FlightPhase.Descent;

            case FlightPhase.TakeoffRoll:
            case FlightPhase.Taxi:
            case FlightPhase.Parked:
            case FlightPhase.LandingRoll:
                // Оторвались от земли (или первое сообщение уже в воздухе).
                return FromGroundOrUnknown(s);

            default:
                return FlightPhase.Climb;
        }
    }

    private FlightPhase FromGroundOrUnknown(AircraftState s)
    {
        if (Phase is FlightPhase.TakeoffRoll or FlightPhase.LandingRoll)
            return FlightPhase.Climb;

        // Первое сообщение в воздухе: решаем по вертикальной скорости и высоте.
        if (s.VerticalSpeedFpm < DescentFpm) return FlightPhase.Descent;
        if (Math.Abs(s.VerticalSpeedFpm) <= ClimbFpm && s.AltitudeFeet >= CruiseMinAltitudeFeet)
            return FlightPhase.Cruise;
        return FlightPhase.Climb;
    }
}
