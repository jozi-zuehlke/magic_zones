using FancyZonesPortable.Core.Engine;
using Xunit;

namespace FancyZonesPortable.Tests.Engine;

public class DeferredActionGateTests
{
    [Fact]
    public void Request_WhenNotDeferred_InvokesFlushImmediately()
    {
        int flushCount = 0;
        var gate = new DeferredActionGate(() => flushCount++);

        gate.Request();

        Assert.Equal(1, flushCount);
    }

    [Fact]
    public void Request_WhenDeferred_DoesNotFlushUntilScopeDisposed()
    {
        int flushCount = 0;
        var gate = new DeferredActionGate(() => flushCount++);

        var scope = gate.Defer();
        gate.Request();

        Assert.Equal(0, flushCount);

        scope.Dispose();

        Assert.Equal(1, flushCount);
    }

    [Fact]
    public void MultipleRequests_WithinDeferredScope_FlushesOnce()
    {
        int flushCount = 0;
        var gate = new DeferredActionGate(() => flushCount++);

        var scope = gate.Defer();
        gate.Request();
        gate.Request();
        gate.Request();

        Assert.Equal(0, flushCount);

        scope.Dispose();

        Assert.Equal(1, flushCount);
    }

    [Fact]
    public void NestedDeferredScopes_FlushesOnceWhenOutermostScopeCompletes()
    {
        int flushCount = 0;
        var gate = new DeferredActionGate(() => flushCount++);

        var outer = gate.Defer();
        gate.Request();

        using (gate.Defer())
        {
            gate.Request();
        }

        Assert.Equal(0, flushCount);

        outer.Dispose();

        Assert.Equal(1, flushCount);
    }

    [Fact]
    public void DeferredScope_WithNoRequests_DoesNotFlush()
    {
        int flushCount = 0;
        var gate = new DeferredActionGate(() => flushCount++);

        using (gate.Defer())
        {
        }

        Assert.Equal(0, flushCount);
    }
}
