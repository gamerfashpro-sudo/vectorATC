using VectorAtc.Core.Ground;
using Xunit;

namespace VectorAtc.Tests;

public class SimulatedGroundServicesTests
{
    private static SimulatedGroundServices Make() => new(new Dictionary<GroundService, TimeSpan>
    {
        [GroundService.Refuel] = TimeSpan.FromSeconds(10),
        [GroundService.Boarding] = TimeSpan.FromSeconds(20),
        [GroundService.Catering] = TimeSpan.FromSeconds(10),
        [GroundService.Baggage] = TimeSpan.FromSeconds(10),
        [GroundService.Deboarding] = TimeSpan.FromSeconds(20),
    });

    private static GroundServiceStatus Get(SimulatedGroundServices s, GroundService g) => s.Statuses.Single(x => x.Service == g);

    [Fact]
    public void Status_says_whether_a_service_can_be_requested()
    {
        var g = Make();
        Assert.True(g.Statuses.All(s => s.CanRequest));

        g.Request(GroundService.Boarding);
        Assert.False(Get(g, GroundService.Boarding).CanRequest);
        Assert.False(Get(g, GroundService.Deboarding).CanRequest);
        Assert.True(Get(g, GroundService.Refuel).CanRequest);

        g.Advance(TimeSpan.FromSeconds(20));
        Assert.True(Get(g, GroundService.Boarding).CanRequest);
        Assert.True(Get(g, GroundService.Deboarding).CanRequest);
    }

    [Fact]
    public void All_services_start_idle()
    {
        Assert.True(Make().Statuses.All(s => s.State == GroundServiceState.Idle && s.Progress == 0));
    }

    [Fact]
    public void Service_progresses_and_completes_with_events_at_start_and_end()
    {
        var g = Make();
        var changes = new List<GroundServiceChange>();
        g.Changed += (_, c) => changes.Add(c);

        Assert.True(g.Request(GroundService.Refuel));
        g.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(0.5, Get(g, GroundService.Refuel).Progress);
        Assert.Equal(GroundServiceState.InProgress, Get(g, GroundService.Refuel).State);

        g.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(GroundServiceState.Completed, Get(g, GroundService.Refuel).State);
        Assert.Equal(1.0, Get(g, GroundService.Refuel).Progress);
        Assert.Equal(
            new[] { GroundServiceState.InProgress, GroundServiceState.Completed },
            changes.Select(c => c.State).ToArray());
    }

    [Fact]
    public void Completed_event_fires_once_even_if_time_keeps_running()
    {
        var g = Make();
        var done = 0;
        g.Changed += (_, c) => { if (c.State == GroundServiceState.Completed) done++; };
        g.Request(GroundService.Catering);
        g.Advance(TimeSpan.FromSeconds(100));
        g.Advance(TimeSpan.FromSeconds(100));
        Assert.Equal(1, done);
    }

    [Fact]
    public void Running_service_cannot_be_requested_again_but_completed_one_can_restart()
    {
        var g = Make();
        Assert.True(g.Request(GroundService.Baggage));
        Assert.False(g.Request(GroundService.Baggage));
        g.Advance(TimeSpan.FromSeconds(10));
        Assert.True(g.Request(GroundService.Baggage));
        Assert.Equal(GroundServiceState.InProgress, Get(g, GroundService.Baggage).State);
        Assert.Equal(0.0, Get(g, GroundService.Baggage).Progress);
    }

    [Fact]
    public void Boarding_and_deboarding_exclude_each_other()
    {
        var g = Make();
        Assert.True(g.Request(GroundService.Boarding));
        Assert.False(g.Request(GroundService.Deboarding));
        g.Advance(TimeSpan.FromSeconds(20));
        Assert.True(g.Request(GroundService.Deboarding));
        Assert.False(g.Request(GroundService.Boarding));
    }

    [Fact]
    public void Zero_or_negative_time_changes_nothing()
    {
        var g = Make();
        g.Request(GroundService.Refuel);
        g.Advance(TimeSpan.Zero);
        g.Advance(TimeSpan.FromSeconds(-3));
        Assert.Equal(0.0, Get(g, GroundService.Refuel).Progress);
    }
}
