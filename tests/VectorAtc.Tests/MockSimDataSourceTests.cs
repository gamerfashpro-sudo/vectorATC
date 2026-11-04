using VectorAtc.Core.Abstractions;
using VectorAtc.Core.Models;
using VectorAtc.SimLink;
using Xunit;

namespace VectorAtc.Tests;

public class MockSimDataSourceTests
{
    [Fact]
    public async Task Connect_raises_Connecting_then_Connected_in_order()
    {
        var sim = new MockSimDataSource();
        var seen = new List<SimConnectionState>();
        sim.ConnectionStateChanged += (_, s) => seen.Add(s);

        await sim.ConnectAsync();

        Assert.Equal(2, seen.Count);
        Assert.Equal(SimConnectionState.Connecting, seen[0]);
        Assert.Equal(SimConnectionState.Connected, seen[1]);
        Assert.Equal(SimConnectionState.Connected, sim.ConnectionState);
    }

    [Fact]
    public void Advance_before_connect_returns_false_and_publishes_nothing()
    {
        var sim = new MockSimDataSource(MockScenarios.ParkedToHoldShort());
        var updates = 0;
        sim.AircraftStateUpdated += (_, _) => updates++;

        Assert.False(sim.Advance());
        Assert.Equal(0, updates);
        Assert.Null(sim.Current);
    }

    [Fact]
    public async Task Advance_publishes_frames_in_script_order_until_exhausted()
    {
        var script = MockScenarios.ParkedToHoldShort();
        var sim = new MockSimDataSource(script);
        var received = new List<AircraftState>();
        sim.AircraftStateUpdated += (_, s) => received.Add(s);
        await sim.ConnectAsync();

        var steps = 0;
        while (sim.Advance()) steps++;

        Assert.Equal(script.Count, steps);
        Assert.Equal(script.Count, received.Count);
        Assert.Equal(script[0], received[0]);
        Assert.Equal(script[^1], sim.Current);
        Assert.False(sim.Advance());
    }

    [Fact]
    public async Task Scenario_ends_stationary_on_ground_with_ground_frequency()
    {
        var sim = new MockSimDataSource(MockScenarios.ParkedToHoldShort());
        await sim.ConnectAsync();
        while (sim.Advance()) { }

        Assert.NotNull(sim.Current);
        Assert.True(sim.Current!.OnGround);
        Assert.Equal(0, sim.Current.GroundSpeedKnots);
        Assert.Equal(121_900, sim.Current.Com1ActiveKhz);
    }

    [Fact]
    public async Task Disconnect_clears_current_and_raises_Disconnected()
    {
        var sim = new MockSimDataSource(MockScenarios.ParkedToHoldShort());
        await sim.ConnectAsync();
        sim.Advance();
        SimConnectionState? last = null;
        sim.ConnectionStateChanged += (_, s) => last = s;

        await sim.DisconnectAsync();

        Assert.Null(sim.Current);
        Assert.Equal(SimConnectionState.Disconnected, last);
    }

    [Fact]
    public async Task Cancelled_token_aborts_connect_without_state_change()
    {
        var sim = new MockSimDataSource();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => sim.ConnectAsync(cts.Token));
        Assert.Equal(SimConnectionState.Disconnected, sim.ConnectionState);
    }

    [Fact]
    public void Push_without_connection_throws()
    {
        var sim = new MockSimDataSource();
        Assert.Throws<InvalidOperationException>(() => sim.Push(MockScenarios.ParkedToHoldShort()[0]));
    }
}
