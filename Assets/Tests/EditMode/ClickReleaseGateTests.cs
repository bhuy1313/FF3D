using NUnit.Framework;
using StarterAssets;

public class ClickReleaseGateTests
{
    [Test]
    public void ShouldProcessClick_AllowsImmediateClick_WhenNotBlocked()
    {
        ClickReleaseGate gate = new ClickReleaseGate();

        Assert.That(gate.ShouldProcessClick(true, true), Is.True);
        Assert.That(gate.IsBlocked, Is.False);
    }

    [Test]
    public void ShouldProcessClick_BlocksUntilButtonReleased()
    {
        ClickReleaseGate gate = new ClickReleaseGate();
        gate.BlockUntilRelease();

        Assert.That(gate.ShouldProcessClick(true, true), Is.False);
        Assert.That(gate.IsBlocked, Is.True);

        Assert.That(gate.ShouldProcessClick(false, false), Is.False);
        Assert.That(gate.IsBlocked, Is.False);

        Assert.That(gate.ShouldProcessClick(true, true), Is.True);
    }

    [Test]
    public void Reset_ClearsBlockedState()
    {
        ClickReleaseGate gate = new ClickReleaseGate();
        gate.BlockUntilRelease();

        gate.Reset();

        Assert.That(gate.IsBlocked, Is.False);
        Assert.That(gate.ShouldProcessClick(true, true), Is.True);
    }
}
