using VectorAtc.Core.Models;

namespace VectorAtc.Core.Traffic;

/// <summary>Источник бортов для карты и таблицы рейсов.</summary>
public interface ITrafficSource
{
    IReadOnlyList<TrafficTarget> Targets { get; }

    /// <summary>Сообщает, сколько времени прошло; источник двигает борты.</summary>
    void Advance(TimeSpan elapsed);
}
