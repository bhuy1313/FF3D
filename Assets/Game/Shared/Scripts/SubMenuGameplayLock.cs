using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public class SubMenuGameplayLock : MonoBehaviour
{
    private static bool suppressNextCursorRestore;

    [Header("SubMenu References")]
    [SerializeField] private SubMenuPanelController subMenuPanelController;
    [SerializeField] private SubMenuEscapeHost subMenuEscapeHost;

    [Header("Gameplay References")]
    [SerializeField] private GameObject playerRoot;
    [SerializeField] private StarterAssetsInputs starterAssetsInputs;
    [SerializeField] private FPSCommandSystem commandSystem;
    [SerializeField] private PlayerActionLock breakActionLock;

    [Header("Cursor")]
    [SerializeField] private bool unlockCursorWhileOpen = true;

    private bool isGameplayLocked;
    private bool restoreCommandSystemEnabled;
    private bool restoreCursorLocked;
    private bool restoreCursorInputForLook;
    private CursorLockMode restoreCursorLockMode;
    private bool restoreCursorVisible;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshLockState();
    }

    private void Update()
    {
        RefreshLockState();
    }

    private void OnDisable()
    {
        ReleaseGameplayLock();
    }

    private void OnDestroy()
    {
        ReleaseGameplayLock();
    }

    private void RefreshLockState()
    {
        bool shouldLock = ShouldLockGameplay();
        if (shouldLock == isGameplayLocked)
        {
            return;
        }

        if (shouldLock)
        {
            AcquireGameplayLock();
        }
        else
        {
            ReleaseGameplayLock();
        }
    }

    private bool ShouldLockGameplay()
    {
        return (subMenuPanelController != null && subMenuPanelController.IsOpen) ||
               (subMenuEscapeHost != null && subMenuEscapeHost.IsSettingsVisible);
    }

    private void AcquireGameplayLock()
    {
        ResolveReferences();

        if (breakActionLock != null)
        {
            breakActionLock.AcquireFullLock();
        }

        if (commandSystem != null)
        {
            restoreCommandSystemEnabled = commandSystem.enabled;
            commandSystem.enabled = false;
        }

        if (starterAssetsInputs != null)
        {
            restoreCursorLocked = starterAssetsInputs.cursorLocked;
            restoreCursorInputForLook = starterAssetsInputs.cursorInputForLook;

            starterAssetsInputs.move = Vector2.zero;
            starterAssetsInputs.look = Vector2.zero;
            starterAssetsInputs.cursorLocked = false;
            starterAssetsInputs.cursorInputForLook = false;
            starterAssetsInputs.ClearGameplayActionInputs();
        }

        restoreCursorLockMode = Cursor.lockState;
        restoreCursorVisible = Cursor.visible;

        if (unlockCursorWhileOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        isGameplayLocked = true;
    }

    private void ReleaseGameplayLock()
    {
        if (!isGameplayLocked)
        {
            return;
        }

        bool skipCursorRestore = suppressNextCursorRestore;
        suppressNextCursorRestore = false;

        if (breakActionLock != null)
        {
            breakActionLock.ReleaseFullLock();
        }

        if (commandSystem != null)
        {
            commandSystem.enabled = restoreCommandSystemEnabled;
        }

        if (starterAssetsInputs != null)
        {
            starterAssetsInputs.move = Vector2.zero;
            starterAssetsInputs.look = Vector2.zero;
            starterAssetsInputs.ClearGameplayActionInputs();

            if (!skipCursorRestore)
            {
                starterAssetsInputs.cursorLocked = restoreCursorLocked;
                starterAssetsInputs.cursorInputForLook = restoreCursorInputForLook;
            }
        }

        if (unlockCursorWhileOpen && !skipCursorRestore)
        {
            Cursor.lockState = restoreCursorLockMode;
            Cursor.visible = restoreCursorVisible;
        }

        RestoreGameplayLookIfNeeded(skipCursorRestore);

        isGameplayLocked = false;
    }

    public static void SuppressNextCursorRestore()
    {
        suppressNextCursorRestore = true;
    }

    private void ResolveReferences()
    {
        if (subMenuPanelController == null)
        {
            subMenuPanelController = GetComponent<SubMenuPanelController>();
        }

        if (subMenuEscapeHost == null)
        {
            subMenuEscapeHost = GetComponent<SubMenuEscapeHost>();
        }

        if (playerRoot == null)
        {
            FirstPersonController firstPersonController = FindAnyObjectByType<FirstPersonController>();
            if (firstPersonController != null)
            {
                playerRoot = firstPersonController.gameObject;
            }
        }

        if (starterAssetsInputs == null && playerRoot != null)
        {
            starterAssetsInputs = playerRoot.GetComponent<StarterAssetsInputs>();
        }

        if (starterAssetsInputs == null)
        {
            starterAssetsInputs = FindAnyObjectByType<StarterAssetsInputs>();
        }

        if (commandSystem == null && playerRoot != null)
        {
            commandSystem = playerRoot.GetComponent<FPSCommandSystem>();
        }

        if (commandSystem == null)
        {
            commandSystem = FindAnyObjectByType<FPSCommandSystem>();
        }

        if (breakActionLock == null && playerRoot != null)
        {
            breakActionLock = PlayerActionLock.GetOrCreate(playerRoot);
        }
    }

    private void RestoreGameplayLookIfNeeded(bool skipCursorRestore)
    {
        if (skipCursorRestore || starterAssetsInputs == null)
        {
            return;
        }

        bool shouldForceGameplayLookRestore =
            !starterAssetsInputs.cursorInputForLook &&
            !ShouldLockGameplay() &&
            Cursor.lockState == CursorLockMode.Locked &&
            !Cursor.visible;

        if (!shouldForceGameplayLookRestore)
        {
            return;
        }

        starterAssetsInputs.cursorInputForLook = true;
        starterAssetsInputs.cursorLocked = true;
    }
}
