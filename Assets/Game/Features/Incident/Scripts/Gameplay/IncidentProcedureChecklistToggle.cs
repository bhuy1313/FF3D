using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class IncidentProcedureChecklistToggle : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private GameObject checklistRoot;
    [SerializeField] private CanvasGroup checklistCanvasGroup;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.P;

    [Header("State")]
    [SerializeField] private bool startVisible = false;

    [Header("Player Lock")]
    [SerializeField] private GameObject playerRoot;
    [SerializeField] private StarterAssetsInputs starterAssetsInputs;
    [SerializeField] private FirstPersonController firstPersonController;
    [SerializeField] private PlayerActionLock playerActionLock;
    [SerializeField] private bool lockCursorWhenVisible = true;
    [SerializeField] private bool lockPlayerMovementWhenVisible = true;

    private bool restoreCursorInputForLook;
    private bool restoreCursorLocked;
    private CursorLockMode restoreCursorLockMode;
    private bool restoreCursorVisible;
    private bool isVisible;

    private void Awake()
    {
        if (checklistRoot == null)
        {
            checklistRoot = gameObject;
        }

        ResolvePlayerReferences();
        CacheCursorState();
        ApplyVisibleState(startVisible);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        bool nextState = !isVisible;
        ApplyVisibleState(nextState);
    }

    public void Show()
    {
        ApplyVisibleState(true);
    }

    public void Hide()
    {
        ApplyVisibleState(false);
    }

    private void ApplyVisibleState(bool visible)
    {
        if (checklistRoot == null)
        {
            return;
        }

        isVisible = visible;

        if (checklistCanvasGroup == null && checklistRoot != null)
        {
            checklistCanvasGroup = checklistRoot.GetComponentInChildren<CanvasGroup>(true);
        }

        if (checklistCanvasGroup != null)
        {
            checklistCanvasGroup.alpha = visible ? 1f : 0f;
            checklistCanvasGroup.interactable = visible;
            checklistCanvasGroup.blocksRaycasts = visible;
        }
        else if (checklistRoot != gameObject)
        {
            checklistRoot.SetActive(visible);
        }

        if (visible)
        {
            ApplyPlayerLock();
            return;
        }

        ReleasePlayerLock();
        RestoreCursorState();
    }

    private void ResolvePlayerReferences()
    {
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
                FirstPersonController sceneController = FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
                if (sceneController != null)
                {
                    firstPersonController = sceneController;
                    playerRoot = sceneController.gameObject;
                }
            }
        }

        if (starterAssetsInputs == null && playerRoot != null)
        {
            starterAssetsInputs = playerRoot.GetComponent<StarterAssetsInputs>();
        }

        if (firstPersonController == null && playerRoot != null)
        {
            firstPersonController = playerRoot.GetComponent<FirstPersonController>();
        }

        if (playerActionLock == null && playerRoot != null)
        {
            playerActionLock = PlayerActionLock.GetOrCreate(playerRoot);
        }
    }

    private void CacheCursorState()
    {
        restoreCursorLockMode = Cursor.lockState;
        restoreCursorVisible = Cursor.visible;

        if (starterAssetsInputs != null)
        {
            restoreCursorLocked = starterAssetsInputs.cursorLocked;
            restoreCursorInputForLook = starterAssetsInputs.cursorInputForLook;
        }
    }

    private void ApplyPlayerLock()
    {
        ResolvePlayerReferences();

        if (lockCursorWhenVisible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (starterAssetsInputs != null)
            {
                starterAssetsInputs.cursorLocked = false;
                starterAssetsInputs.cursorInputForLook = false;
                starterAssetsInputs.ClearGameplayActionInputs();
            }
        }

        if (lockPlayerMovementWhenVisible && playerActionLock != null)
        {
            playerActionLock.AcquireFullLock();
        }
    }

    private void ReleasePlayerLock()
    {
        if (lockPlayerMovementWhenVisible && playerActionLock != null)
        {
            playerActionLock.ReleaseFullLock();
        }
    }

    private void RestoreCursorState()
    {
        if (!lockCursorWhenVisible)
        {
            return;
        }

        Cursor.lockState = restoreCursorLockMode;
        Cursor.visible = restoreCursorVisible;
        if (starterAssetsInputs != null)
        {
            starterAssetsInputs.cursorLocked = restoreCursorLocked;
            starterAssetsInputs.cursorInputForLook = restoreCursorInputForLook;
            starterAssetsInputs.ClearGameplayActionInputs();
        }
    }
}
