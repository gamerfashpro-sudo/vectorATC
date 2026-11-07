namespace VectorAtc.Core.Ground;

/// <summary>
/// Встроенная имитация наземного обслуживания: услуга идёт заданное время и заканчивается. Это модель, а не данные
/// симулятора: ничего не меняется ни в самолёте, ни в мире. Посадка и высадка пассажиров не могут идти одновременно.
/// </summary>
public sealed class SimulatedGroundServices : IGroundServicesProvider
{
    private static readonly IReadOnlyDictionary<GroundService, TimeSpan> DefaultDurations = new Dictionary<GroundService, TimeSpan>
    {
        [GroundService.Refuel] = TimeSpan.FromSeconds(90),
        [GroundService.Boarding] = TimeSpan.FromSeconds(120),
        [GroundService.Catering] = TimeSpan.FromSeconds(60),
        [GroundService.Baggage] = TimeSpan.FromSeconds(75),
        [GroundService.Deboarding] = TimeSpan.FromSeconds(100),
    };

    private readonly IReadOnlyDictionary<GroundService, TimeSpan> _durations;
    private readonly Dictionary<GroundService, (GroundServiceState State, TimeSpan Elapsed)> _services = new();

    public SimulatedGroundServices(IReadOnlyDictionary<GroundService, TimeSpan>? durations = null)
    {
        _durations = durations ?? DefaultDurations;
        foreach (var service in Enum.GetValues<GroundService>())
            _services[service] = (GroundServiceState.Idle, TimeSpan.Zero);
    }

    public event EventHandler<GroundServiceChange>? Changed;

    public IReadOnlyList<GroundServiceStatus> Statuses =>
        Enum.GetValues<GroundService>().Select(Status).ToList();

    public bool Request(GroundService service)
    {
        if (_services[service].State == GroundServiceState.InProgress)
            return false;
        if (Opposite(service) is { } other && _services[other].State == GroundServiceState.InProgress)
            return false;

        _services[service] = (GroundServiceState.InProgress, TimeSpan.Zero);
        Changed?.Invoke(this, new GroundServiceChange(service, GroundServiceState.InProgress));
        return true;
    }

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
            return;

        foreach (var service in Enum.GetValues<GroundService>())
        {
            var (state, spent) = _services[service];
            if (state != GroundServiceState.InProgress)
                continue;

            spent += elapsed;
            if (spent >= _durations[service])
            {
                _services[service] = (GroundServiceState.Completed, _durations[service]);
                Changed?.Invoke(this, new GroundServiceChange(service, GroundServiceState.Completed));
            }
            else
            {
                _services[service] = (state, spent);
            }
        }
    }

    private GroundServiceStatus Status(GroundService service)
    {
        var (state, spent) = _services[service];
        var progress = state switch
        {
            GroundServiceState.Completed => 1.0,
            GroundServiceState.InProgress => Math.Min(1.0, spent.TotalSeconds / _durations[service].TotalSeconds),
            _ => 0.0,
        };
        return new GroundServiceStatus(service, state, progress);
    }

    private static GroundService? Opposite(GroundService service) => service switch
    {
        GroundService.Boarding => GroundService.Deboarding,
        GroundService.Deboarding => GroundService.Boarding,
        _ => null,
    };
}
