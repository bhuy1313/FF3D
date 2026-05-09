using StarterAssets;
using UnityEngine;

public class TargetToggleUI : MonoBehaviour
{
    private enum CrosshairState
    {
        Default,
        Locked,
        Rescuable,
        IsolationDevice,
        Interact,
        Grab,
        Pickup,
        Climb
    }

    [Header("Target Source")]
    [SerializeField] private FPSInteractionSystem interactionSystem;
    [SerializeField] private GameObject targetOverride;

    [Header("Crosshair Icons")]
    [SerializeField] private GameObject defaultIcon;
    [SerializeField] private GameObject lockedIcon;
    [SerializeField] private GameObject rescuableIcon;
    [SerializeField] private GameObject isolationDeviceIcon;
    [SerializeField] private GameObject interactIcon;
    [SerializeField] private GameObject grabIcon;
    [SerializeField] private GameObject pickupIcon;
    [SerializeField] private GameObject climbIcon;

    [Header("Border")]
    [SerializeField] private RectTransform borderFrame;
    [SerializeField] private Vector2 defaultBorderSize = new Vector2(24f, 24f);
    [SerializeField] private Vector2 interactionBorderSize = new Vector2(80f, 80f);
    [SerializeField] private GameObject crosshairRoot;

    private CrosshairState currentState = (CrosshairState)(-1);
    private bool hideWhileContinuousAction;

    private void Awake()
    {
        ResolveInteractionSystem();
        ResolveBorderFrame();
        ResolveCrosshairRoot();
        PlayerContinuousActionBus.OnActionStarted += HandleActionStarted;
        PlayerContinuousActionBus.OnActionEnded += HandleActionEnded;
    }

    private void OnDestroy()
    {
        PlayerContinuousActionBus.OnActionStarted -= HandleActionStarted;
        PlayerContinuousActionBus.OnActionEnded -= HandleActionEnded;
    }

    private void Update()
    {
        ResolveInteractionSystem();
        ResolveBorderFrame();
        ResolveCrosshairRoot();

        if (hideWhileContinuousAction)
        {
            SetCrosshairRootVisible(false);
            return;
        }

        SetCrosshairRootVisible(true);
        SetState(ResolveState());
    }

    private void ResolveInteractionSystem()
    {
        if (interactionSystem == null)
        {
            interactionSystem = FindAnyObjectByType<FPSInteractionSystem>();
        }
    }

    private void ResolveBorderFrame()
    {
        if (borderFrame != null)
        {
            return;
        }

        borderFrame = ResolveCommonParentRect(
            defaultIcon,
            lockedIcon,
            rescuableIcon,
            isolationDeviceIcon,
            interactIcon,
            grabIcon,
            pickupIcon,
            climbIcon);
    }

    private void ResolveCrosshairRoot()
    {
        if (crosshairRoot != null)
        {
            return;
        }

        if (borderFrame != null)
        {
            crosshairRoot = borderFrame.gameObject;
            return;
        }

        if (defaultIcon != null)
        {
            crosshairRoot = defaultIcon.transform.parent != null
                ? defaultIcon.transform.parent.gameObject
                : defaultIcon;
        }
    }

    private CrosshairState ResolveState()
    {
        if (interactionSystem != null)
        {
            return interactionSystem.CurrentFocusKind switch
            {
                FPSInteractionSystem.InteractionFocusKind.Locked => CrosshairState.Locked,
                FPSInteractionSystem.InteractionFocusKind.Rescuable => CrosshairState.Rescuable,
                FPSInteractionSystem.InteractionFocusKind.IsolationDevice => CrosshairState.IsolationDevice,
                FPSInteractionSystem.InteractionFocusKind.Interact => CrosshairState.Interact,
                FPSInteractionSystem.InteractionFocusKind.Grab => CrosshairState.Grab,
                FPSInteractionSystem.InteractionFocusKind.Pickup => CrosshairState.Pickup,
                FPSInteractionSystem.InteractionFocusKind.Climb => CrosshairState.Climb,
                _ => CrosshairState.Default
            };
        }

        return targetOverride != null ? CrosshairState.Interact : CrosshairState.Default;
    }

    private void SetState(CrosshairState nextState)
    {
        if (currentState == nextState)
        {
            return;
        }

        currentState = nextState;
        ApplyBorderSize(nextState);
        SetVisible(defaultIcon, nextState == CrosshairState.Default);
        SetVisible(lockedIcon, nextState == CrosshairState.Locked);
        SetVisible(rescuableIcon, nextState == CrosshairState.Rescuable);
        SetVisible(isolationDeviceIcon, nextState == CrosshairState.IsolationDevice);
        SetVisible(climbIcon, nextState == CrosshairState.Climb);

        bool useSharedHandIcon =
            interactIcon != null &&
            (ReferenceEquals(interactIcon, grabIcon) ||
             ReferenceEquals(interactIcon, pickupIcon) ||
             ReferenceEquals(grabIcon, pickupIcon));

        if (useSharedHandIcon)
        {
            SetVisible(
                interactIcon,
                nextState == CrosshairState.Interact ||
                nextState == CrosshairState.Grab ||
                nextState == CrosshairState.Pickup);
            return;
        }

        SetVisible(interactIcon, nextState == CrosshairState.Interact);
        SetVisible(grabIcon, nextState == CrosshairState.Grab);
        SetVisible(pickupIcon, nextState == CrosshairState.Pickup);
    }

    private static void SetVisible(GameObject target, bool visible)
    {
        if (target != null && target.activeSelf != visible)
        {
            target.SetActive(visible);
        }
    }

    private void ApplyBorderSize(CrosshairState state)
    {
        if (borderFrame == null)
        {
            return;
        }

        Vector2 targetSize = state == CrosshairState.Default
            ? defaultBorderSize
            : interactionBorderSize;

        if (borderFrame.sizeDelta != targetSize)
        {
            borderFrame.sizeDelta = targetSize;
        }
    }

    private static RectTransform ResolveCommonParentRect(params GameObject[] icons)
    {
        for (int i = 0; i < icons.Length; i++)
        {
            GameObject icon = icons[i];
            if (icon == null)
            {
                continue;
            }

            if (icon.transform.parent is RectTransform parentRect)
            {
                return parentRect;
            }
        }

        return null;
    }

    private void HandleActionStarted(string actionText)
    {
        hideWhileContinuousAction = true;
        SetCrosshairRootVisible(false);
    }

    private void HandleActionEnded(bool success)
    {
        hideWhileContinuousAction = false;
        SetCrosshairRootVisible(true);
        currentState = (CrosshairState)(-1);
    }

    private void SetCrosshairRootVisible(bool visible)
    {
        if (crosshairRoot != null && crosshairRoot.activeSelf != visible)
        {
            crosshairRoot.SetActive(visible);
        }
    }
}
