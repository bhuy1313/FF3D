namespace StarterAssets
{
    public sealed class ClickReleaseGate
    {
        public bool IsBlocked { get; private set; }

        public void BlockUntilRelease()
        {
            IsBlocked = true;
        }

        public void Reset()
        {
            IsBlocked = false;
        }

        public bool ShouldProcessClick(bool isButtonDown, bool isButtonHeld)
        {
            if (!IsBlocked)
            {
                return isButtonDown;
            }

            if (isButtonHeld)
            {
                return false;
            }

            IsBlocked = false;
            return false;
        }
    }
}
