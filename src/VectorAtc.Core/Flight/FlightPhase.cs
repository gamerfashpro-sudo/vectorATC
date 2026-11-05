namespace VectorAtc.Core.Flight;

public enum FlightPhase
{
    /// <summary>На земле, наземная скорость меньше 1 узла.</summary>
    Parked,
    Taxi,
    TakeoffRoll,
    Climb,
    Cruise,
    Descent,
    /// <summary>Пробег после посадки (на земле, скорость не меньше порога взлёта).</summary>
    LandingRoll
}
