namespace VectorAtc.Core.Ground;

public enum GroundService
{
    Refuel,
    Boarding,
    Catering,
    Baggage,
    Deboarding
}

public enum GroundServiceState
{
    Idle,
    InProgress,
    Completed
}

/// <summary>Состояние одной услуги. Progress от 0 до 1.</summary>
/// <summary>Состояние услуги. CanRequest: можно ли заказать её прямо сейчас (не идёт сама и не мешает другая услуга).</summary>
public sealed record GroundServiceStatus(GroundService Service, GroundServiceState State, double Progress, bool CanRequest = true);

/// <summary>Изменение состояния услуги (началась или закончилась).</summary>
public sealed record GroundServiceChange(GroundService Service, GroundServiceState State);

/// <summary>
/// Поставщик наземного обслуживания. Приложение работает только с этим интерфейсом, поэтому встроенную имитацию
/// можно заменить другой службой, не трогая окно и журнал.
/// </summary>
public interface IGroundServicesProvider
{
    IReadOnlyList<GroundServiceStatus> Statuses { get; }

    /// <summary>Заказывает услугу. false, если её сейчас нельзя начать (уже идёт или мешает другая).</summary>
    bool Request(GroundService service);

    /// <summary>Сообщает, сколько времени прошло; провайдер двигает свои услуги.</summary>
    void Advance(TimeSpan elapsed);

    event EventHandler<GroundServiceChange>? Changed;
}
