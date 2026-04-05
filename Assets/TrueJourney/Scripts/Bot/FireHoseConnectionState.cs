namespace TrueJourney.BotBehavior
{
    public sealed class FireHoseConnectionState
    {
        public object ConnectedSource { get; private set; }
        public bool IsConnected => ConnectedSource != null;

        public bool TryConnect(object source)
        {
            if (source == null)
            {
                return false;
            }

            ConnectedSource = source;
            return true;
        }

        public bool TryDisconnect(object source = null)
        {
            if (!IsConnected)
            {
                return false;
            }

            if (source != null && !ReferenceEquals(source, ConnectedSource))
            {
                return false;
            }

            ConnectedSource = null;
            return true;
        }

        public bool CanUse(bool sourceProvidesSupply, bool requiresConnection, bool hasLocalSupply)
        {
            if (IsConnected && sourceProvidesSupply)
            {
                return true;
            }

            if (!requiresConnection)
            {
                return hasLocalSupply;
            }

            return false;
        }
    }
}
