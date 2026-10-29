using ClusterSim.Core.Simulation.Engine;
using Xunit;

namespace ClusterSim.Tests.Simulation.Engine;

public class EventQueueTests
{
    [Fact]
    public void Dequeue_ReturnsEventsInTimeOrder()
    {
        var queue = new EventQueue();
        queue.Enqueue(new SimulationEvent(30, 1, SimulationEventType.JobFinished));
        queue.Enqueue(new SimulationEvent(10, 2, SimulationEventType.JobSubmitted));
        queue.Enqueue(new SimulationEvent(20, 3, SimulationEventType.JobSubmitted));

        Assert.Equal(10, queue.Dequeue().Time);
        Assert.Equal(20, queue.Dequeue().Time);
        Assert.Equal(30, queue.Dequeue().Time);
        Assert.True(queue.IsEmpty);
    }

    [Fact]
    public void SameTime_UsesSequenceOrder()
    {
        var queue = new EventQueue();
        queue.Enqueue(new SimulationEvent(5, 9, SimulationEventType.JobFinished));
        queue.Enqueue(new SimulationEvent(5, 3, SimulationEventType.JobStarted));

        Assert.Equal(3, queue.Dequeue().Sequence);
        Assert.Equal(9, queue.Dequeue().Sequence);
    }

    [Fact]
    public void TryPeek_DoesNotRemoveEvent()
    {
        var queue = new EventQueue();
        queue.Enqueue(new SimulationEvent(7, 1, SimulationEventType.JobSubmitted));

        Assert.True(queue.TryPeek(out var peeked));
        Assert.Equal(7, peeked!.Time);
        Assert.Equal(1, queue.Count);
    }
}