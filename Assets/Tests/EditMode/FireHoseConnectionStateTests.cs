using NUnit.Framework;
using TrueJourney.BotBehavior;

public class FireHoseConnectionStateTests
{
    [Test]
    public void TryConnect_StoresConnectedSource()
    {
        FireHoseConnectionState state = new FireHoseConnectionState();
        object source = new object();

        Assert.That(state.TryConnect(source), Is.True);
        Assert.That(state.IsConnected, Is.True);
        Assert.That(state.ConnectedSource, Is.SameAs(source));
    }

    [Test]
    public void TryDisconnect_OnlyClearsMatchingSourceWhenProvided()
    {
        FireHoseConnectionState state = new FireHoseConnectionState();
        object source = new object();
        state.TryConnect(source);

        Assert.That(state.TryDisconnect(new object()), Is.False);
        Assert.That(state.IsConnected, Is.True);

        Assert.That(state.TryDisconnect(source), Is.True);
        Assert.That(state.IsConnected, Is.False);
        Assert.That(state.ConnectedSource, Is.Null);
    }

    [Test]
    public void CanUse_ReturnsTrueForConnectedPressurizedSupply()
    {
        FireHoseConnectionState state = new FireHoseConnectionState();
        state.TryConnect(new object());

        Assert.That(state.CanUse(
            sourceProvidesSupply: true,
            requiresConnection: true,
            hasLocalSupply: false), Is.True);
    }

    [Test]
    public void CanUse_RequiresSupplyWhenConnectionIsMandatory()
    {
        FireHoseConnectionState state = new FireHoseConnectionState();

        Assert.That(state.CanUse(
            sourceProvidesSupply: false,
            requiresConnection: true,
            hasLocalSupply: true), Is.False);
    }

    [Test]
    public void CanUse_FallsBackToLocalSupplyWhenConnectionIsOptional()
    {
        FireHoseConnectionState state = new FireHoseConnectionState();

        Assert.That(state.CanUse(
            sourceProvidesSupply: false,
            requiresConnection: false,
            hasLocalSupply: true), Is.True);
    }
}
