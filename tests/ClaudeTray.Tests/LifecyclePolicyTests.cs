using ClaudeTray.Model;

namespace ClaudeTray.Tests;

public class LifecyclePolicyTests
{
    [Fact]
    public void Grace_NoExitBefore5s()
    {
        var p = new LifecyclePolicy(launchedAtMs: 0);
        Assert.False(p.ShouldExit(1000, 0));
        Assert.False(p.ShouldExit(4999, 0));
    }

    [Fact]
    public void Grace_DoesNotCountTowardsEmptyDelay()
    {
        // Vrijeme bez sesija broji se tek nakon 5 s mirovanja po pokretanju, kao u originalu.
        var p = new LifecyclePolicy(launchedAtMs: 0);
        Assert.False(p.ShouldExit(1000, 0));
        Assert.False(p.ShouldExit(5000, 0));
        Assert.False(p.ShouldExit(7999, 0));
        Assert.True(p.ShouldExit(8000, 0));
    }

    [Fact]
    public void EmptyNeedsFull3s()
    {
        var p = new LifecyclePolicy(launchedAtMs: 0);
        Assert.False(p.ShouldExit(6000, 0));
        Assert.False(p.ShouldExit(8999, 0));
        Assert.True(p.ShouldExit(9000, 0));
    }

    [Fact]
    public void LiveSessionResetsTimer()
    {
        var p = new LifecyclePolicy(launchedAtMs: 0);
        Assert.False(p.ShouldExit(6000, 0));
        Assert.False(p.ShouldExit(7000, 1));
        Assert.False(p.ShouldExit(9500, 0));
        Assert.True(p.ShouldExit(12500, 0));
    }
}
