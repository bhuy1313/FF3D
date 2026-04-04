using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using System.Reflection;

[DisallowMultipleComponent]
public class CallPhaseDeskInteractTrigger : MonoBehaviour, IInteractable
{
    [Header("Call Phase Canvas")]
    [SerializeField] private GameObject callPhaseCanvas;
    [SerializeField] private CanvasGroup callPhaseCanvasGroup;

    [Header("Camera Switch")]
    [SerializeField] private CinemachineCamera playerFollowCamera;
    [SerializeField] private CinemachineCamera deskCamera;
    [SerializeField] private CinemachineBrain mainCameraBrain;
    [SerializeField] private bool disablePlayerFollowCameraOnOpen = true;
    [SerializeField] private bool enableDeskCameraOnOpen = true;
    [SerializeField] private bool waitForCameraBlendBeforeOpeningCanvas = true;
    [SerializeField] private float fallbackCameraBlendDurationSeconds = 2f;
    [SerializeField] private float maxCameraBlendWaitSeconds = 5f;

    [Header("Gameplay Lock")]
    [SerializeField] private GameObject playerRoot;
    [SerializeField] private BreakActionLock breakActionLock;
    [SerializeField] private StarterAssetsInputs starterAssetsInputs;
    [SerializeField] private FPSCommandSystem commandSystem;
    [SerializeField] private bool unlockCursorWhileOpen = true;

    [Header("Behavior")]
    [SerializeField] private float openDelaySeconds = 0f;
    [SerializeField] private bool allowRetrigger = false;

    private bool hasOpened;
    private bool isOpening;
    private Coroutine openRoutine;

    private void Awake()
    {
        ResolveReferences(null);
    }

    public void Interact(GameObject interactor)
    {
        if (isOpening || (hasOpened && !allowRetrigger))
        {
            return;
        }

        ResolveReferences(interactor);
        isOpening = true;
        openRoutine = StartCoroutine(OpenAfterDelayRoutine());
    }

    private System.Collections.IEnumerator OpenAfterDelayRoutine()
    {
        if (breakActionLock != null)
        {
            breakActionLock.Acquire();
        }

        if (commandSystem != null)
        {
            commandSystem.enabled = false;
        }

        if (starterAssetsInputs != null)
        {
            starterAssetsInputs.move = Vector2.zero;
            starterAssetsInputs.look = Vector2.zero;
            starterAssetsInputs.cursorLocked = false;
            starterAssetsInputs.cursorInputForLook = false;
            starterAssetsInputs.ClearGameplayActionInputs();
        }

        if (unlockCursorWhileOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (disablePlayerFollowCameraOnOpen && playerFollowCamera != null)
        {
            playerFollowCamera.gameObject.SetActive(false);
        }

        if (enableDeskCameraOnOpen && deskCamera != null)
        {
            deskCamera.gameObject.SetActive(true);
        }

        if (waitForCameraBlendBeforeOpeningCanvas)
        {
            yield return WaitForCameraBlendToFinish();
        }

        float postBlendDelay = Mathf.Max(0f, openDelaySeconds);
        if (postBlendDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(postBlendDelay);
        }

        if (callPhaseCanvas != null)
        {
            callPhaseCanvas.SetActive(true);
        }

        if (callPhaseCanvasGroup != null)
        {
            callPhaseCanvasGroup.alpha = 1f;
            callPhaseCanvasGroup.interactable = true;
            callPhaseCanvasGroup.blocksRaycasts = true;
        }

        hasOpened = true;
        isOpening = false;
        openRoutine = null;
    }

    private void OnDisable()
    {
        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        if (!hasOpened)
        {
            isOpening = false;
        }
    }

    private void ResolveReferences(GameObject interactor)
    {
        if (callPhaseCanvas == null)
        {
            callPhaseCanvas = FindGameObjectByName("CallPhaseCanvas");
        }

        if (callPhaseCanvasGroup == null && callPhaseCanvas != null)
        {
            callPhaseCanvasGroup = callPhaseCanvas.GetComponent<CanvasGroup>();
        }

        if (playerFollowCamera == null)
        {
            playerFollowCamera = FindCinemachineCamera("PlayerFollowCamera");
        }

        if (deskCamera == null)
        {
            deskCamera = FindCinemachineCamera("Desk Camera");
        }

        if (mainCameraBrain == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCameraBrain = mainCamera.GetComponent<CinemachineBrain>();
            }

            if (mainCameraBrain == null)
            {
                mainCameraBrain = FindFirstObjectByType<CinemachineBrain>(FindObjectsInactive.Include);
            }
        }

        if (playerRoot == null)
        {
            if (interactor != null)
            {
                playerRoot = interactor;
            }
            else
            {
                FirstPersonController firstPersonController = FindFirstObjectByType<FirstPersonController>();
                if (firstPersonController != null)
                {
                    playerRoot = firstPersonController.gameObject;
                }
            }
        }

        if (breakActionLock == null && playerRoot != null)
        {
            breakActionLock = BreakActionLock.GetOrCreate(playerRoot);
        }

        if (starterAssetsInputs == null && playerRoot != null)
        {
            starterAssetsInputs = playerRoot.GetComponent<StarterAssetsInputs>();
        }

        if (commandSystem == null && playerRoot != null)
        {
            commandSystem = playerRoot.GetComponent<FPSCommandSystem>();
        }
    }

    private System.Collections.IEnumerator WaitForCameraBlendToFinish()
    {
        yield return null;

        float fallbackDuration = Mathf.Max(0f, ResolveDefaultBrainBlendDuration());
        if (mainCameraBrain == null)
        {
            if (fallbackDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(fallbackDuration);
            }

            yield break;
        }

        float startTime = Time.unscaledTime;
        float timeout = startTime + Mathf.Max(maxCameraBlendWaitSeconds, fallbackDuration + 0.25f);
        bool sawBlend = false;

        while (Time.unscaledTime < timeout)
        {
            object activeBlend = GetActiveBlend(mainCameraBrain);
            if (activeBlend != null)
            {
                sawBlend = true;
                yield return null;
                continue;
            }

            if (sawBlend)
            {
                yield break;
            }

            if (fallbackDuration <= 0f)
            {
                yield break;
            }

            if (Time.unscaledTime - startTime >= fallbackDuration)
            {
                yield break;
            }

            yield return null;
        }
    }

    private float ResolveDefaultBrainBlendDuration()
    {
        if (mainCameraBrain == null)
        {
            return fallbackCameraBlendDurationSeconds;
        }

        object blendDefinition = GetMemberValue(mainCameraBrain, "DefaultBlend");
        float duration = GetFloatMemberValue(blendDefinition, "Time");
        if (duration > 0f)
        {
            return duration;
        }

        return fallbackCameraBlendDurationSeconds;
    }

    private static object GetActiveBlend(CinemachineBrain brain)
    {
        return GetMemberValue(brain, "ActiveBlend");
    }

    private static object GetMemberValue(object source, string memberName)
    {
        if (source == null || string.IsNullOrWhiteSpace(memberName))
        {
            return null;
        }

        System.Type sourceType = source.GetType();
        PropertyInfo property = sourceType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null)
        {
            return property.GetValue(source);
        }

        FieldInfo field = sourceType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (field != null)
        {
            return field.GetValue(source);
        }

        return null;
    }

    private static float GetFloatMemberValue(object source, string memberName)
    {
        object memberValue = GetMemberValue(source, memberName);
        if (memberValue is float floatValue)
        {
            return floatValue;
        }

        return 0f;
    }

    private static GameObject FindGameObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] sceneTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            Transform candidate = sceneTransforms[i];
            if (candidate != null && string.Equals(candidate.name, objectName, System.StringComparison.Ordinal))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static CinemachineCamera FindCinemachineCamera(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        CinemachineCamera[] cameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera candidate = cameras[i];
            if (candidate != null && string.Equals(candidate.name, objectName, System.StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }
}
