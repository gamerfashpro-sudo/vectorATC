using VectorAtc.Core.Controller;
using VectorAtc.Core.Parsing;
using Xunit;

namespace VectorAtc.Tests;

public class RequestQueueTests
{
    private static PilotRequest Req(string callsign, IntentType intent) => new(callsign, intent, callsign);

    private static string Drain(RequestQueue queue)
    {
        var order = new List<string>();
        while (queue.TryDequeue(out var r)) order.Add(r!.Callsign!);
        return string.Join(",", order);
    }

    [Fact]
    public void Emergency_is_served_before_earlier_routine_requests()
    {
        var queue = new RequestQueue();
        queue.Enqueue(Req("A1", IntentType.RequestTaxi));
        queue.Enqueue(Req("B2", IntentType.RadioCheck));
        queue.Enqueue(Req("C3", IntentType.Emergency));

        Assert.Equal("C3,A1,B2", Drain(queue));
    }

    [Fact]
    public void Same_priority_is_first_in_first_out()
    {
        var queue = new RequestQueue();
        queue.Enqueue(Req("A1", IntentType.Emergency));
        queue.Enqueue(Req("B2", IntentType.Emergency));
        queue.Enqueue(Req("C3", IntentType.RequestTaxi));
        queue.Enqueue(Req("D4", IntentType.RequestTaxi));

        Assert.Equal("A1,B2,C3,D4", Drain(queue));
    }

    [Fact]
    public void Empty_queue_returns_false()
    {
        var queue = new RequestQueue();
        Assert.False(queue.TryDequeue(out var r));
        Assert.Null(r);
        Assert.Equal(0, queue.Count);
    }
}
