using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public class GameMasterUiMovementInputLock : MonoBehaviour
{
    [Header("UI Sources")]
    [SerializeField] private GameObject[] blockingUiRoots;
    [SerializeField] private DispatchNotesUIController dispatchNotesUI;
    [SerializeField] private SubMenuPanelController subMenuPanelController;
    [SerializeField] private SubMenuEscapeHost subMenuEscapeHost;

    [Header("Player References")]
    [SerializeField] private GameObject playerRoot;
    [SerializeField] private StarterAssetsInputs starterAssetsInputs;
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private PlayerActionLock playerActionLock;

    [Header("Lock Settings")]
    [SerializeField] private bool disableFirstPersonController = false;
    [SerializeField] private bool usePlayerActionLock = true;
    [SerializeField] private bool disableLookInput = true;

    private bool isMovementInputLocked;
    private bool restoreFirstPersonControllerEnabled;
    private bool restoreCursorInputForLook;

    public bool IsMovementInputLocked => isMovementInputLocked;

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
        ReleaseMovementInputLock();
    }

    private void OnDestroy()
    {
        ReleaseMovementInputLock();
    }

    private void RefreshLockState()
    {
        bool shouldLock = ShouldLockMovementInput();
        if (shouldLock == isMovementInputLocked)
        {
            return;
        }

        if (shouldLock)
        {
            AcquireMovementInputLock();
            return;
        }

        ReleaseMovementInputLock();
    }

    private bool ShouldLockMovementInput()
    {
        if (dispatchNotesUI != null && dispatchNotesUI.IsNotesOpenRuntime)
        {
            return true;
        }

        if (subMenuPanelController != null && subMenuPanelController.IsOpen)
        {
            return true;
        }

        if (subMenuEscapeHost != null && subMenuEscapeHost.IsSettingsVisible)
        {
            return true;
        }

        if (blockingUiRoots == null || blockingUiRoots.Length <= 0)
        {
            return false;
        }

        for (int i = 0; i < blockingUiRoots.Length; i++)
        {
            GameObject uiRoot = blockingUiRoots[i];
            if (uiRoot != null && uiRoot.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private void AcquireMovementInputLock()
    {
        ResolveReferences();

        if (starterAssetsInputs != null)
        {
            restoreCursorInputForLook = starterAssetsInputs.cursorInputForLook;
            starterAssetsInputs.move = Vector2.zero;
            starterAssetsInputs.look = Vector2.zero;
            starterAssetsInputs.jump = false;
            starterAssetsInputs.sprint = false;
            starterAssetsInputs.crouch = false;
            starterAssetsInputs.ClearGameplayActionInputs();

            if (disableLookInput)
            {
                starterAssetsInputs.cursorInputForLook = false;
            }
        }

        if (usePlayerActionLock && playerActionLock != null)
        {
            playerActionLock.AcquireFullLock();
        }
        else if (disableFirstPersonController && firstPersonController != null)
        {
            restoreFirstPersonControllerEnabled = firstPersonController.enabled;
            firstPersonController.enabled = false;
        }

        isMovementInputLocked = true;
    }

    private void ReleaseMovementInputLock()
    {
        if (!isMovementInputLocked)
        {
            return;
        }

        if (usePlayerActionLock && playerActionLock != null)
        {
            playerActionLock.ReleaseFullLock();
        }
        else if (disableFirstPersonController && firstPersonController != null)
        {
            firstPersonController.enabled = restoreFirstPersonControllerEnabled;
        }

        if (starterAssetsInputs != null)
        {
            starterAssetsInputs.move = Vector2.zero;
            starterAssetsInputs.look = Vector2.zero;
            starterAssetsInputs.jump = false;
            starterAssetsInputs.sprint = false;
            starterAssetsInputs.crouch = false;
            starterAssetsInputs.ClearGameplayActionInputs();

            if (disableLookInput)
            {
                bool shouldForceGameplayLookRestore =
                    !restoreCursorInputForLook &&
                    Cursor.lockState == CursorLockMode.Locked &&
                    !Cursor.visible;

                starterAssetsInputs.cursorInputForLook = restoreCursorInputForLook || shouldForceGameplayLookRestore;
                if (shouldForceGameplayLookRestore)
                {
                    starterAssetsInputs.cursorLocked = true;
                }
            }
        }

        isMovementInputLocked = false;
    }

    private void ResolveReferences()
    {
        if (dispatchNotesUI == null)
        {
            dispatchNotesUI = FindAnyObjectByType<DispatchNotesUIController>(FindObjectsInactive.Include);
        }

        if (subMenuPanelController == null)
        {
            subMenuPanelController = FindAnyObjectByType<SubMenuPanelController>(FindObjectsInactive.Include);
        }

        if (subMenuEscapeHost == null)
        {
            subMenuEscapeHost = FindAnyObjectByType<SubMenuEscapeHost>(FindObjectsInactive.Include);
        }

        if (playerRoot == null)
        {
            if (firstPersonController != null)
            {
                playerRoot = firstPersonController.gameObject;
            }
            else if (starterAssetsInputs != null)
            {
                playerRoot = starterAssetsInputs.gameObject;
            }
            else
            {
                FirstPersonController sceneFirstPersonController = FindAnyObjectByType<FirstPersonController>();
                if (sceneFirstPersonController != null)
                {
                    firstPersonController = sceneFirstPersonController;
                    playerRoot = sceneFirstPersonController.gameObject;
                }
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

        if (firstPersonController == null && playerRoot != null)
        {
            firstPersonController = playerRoot.GetComponent<FirstPersonController>();
        }

        if (firstPersonController == null)
        {
            firstPersonController = FindAnyObjectByType<FirstPersonController>();
        }

        if (playerActionLock == null)
        {
            if (playerRoot != null)
            {
                playerActionLock = PlayerActionLock.GetOrCreate(playerRoot);
            }
            else if (firstPersonController != null)
            {
                playerActionLock = PlayerActionLock.GetOrCreate(firstPersonController.gameObject);
            }
        }
    }
}
