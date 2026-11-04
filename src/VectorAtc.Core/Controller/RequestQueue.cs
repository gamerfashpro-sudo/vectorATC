using VectorAtc.Core.Parsing;

namespace VectorAtc.Core.Controller;

/// <summary>Очередь запросов: аварийные обслуживаются раньше обычных, внутри приоритета по порядку поступления.</summary>
public sealed class RequestQueue
{
    private readonly PriorityQueue<PilotRequest, (int Priority, long Sequence)> _queue = new();
    private long _sequence;

    public int Count => _queue.Count;

    public void Enqueue(PilotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var priority = request.Intent == IntentType.Emergency ? 0 : 1;
        _queue.Enqueue(request, (priority, _sequence++));
    }

    public bool TryDequeue(out PilotRequest? request) => _queue.TryDequeue(out request, out _);
}
