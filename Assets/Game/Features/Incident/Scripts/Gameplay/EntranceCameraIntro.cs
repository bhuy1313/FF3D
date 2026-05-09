using System.Collections;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;

public enum IntroPreset
{
    Custom,
    GetOutOfCar
}

[DisallowMultipleComponent]
public class EntranceCameraIntro : MonoBehaviour
{
    [Header("Preset")]
    [SerializeField] private IntroPreset preset = IntroPreset.Custom;

    [Header("Flow")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool onlyOncePerSceneLoad = true;
    [SerializeField] private float resolveTargetTimeout = 2f;
    [SerializeField] private float startDelay = 0.15f;
    [SerializeField] private float duration = 2.6f;
    [SerializeField] private float blendInTime = 0.35f;
    [SerializeField] private float blendOutTime = 0.45f;
    [SerializeField] private AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private int introPriority = 100;

    [Header("Shot (Used by Custom Preset)")]
    [SerializeField] private Transform pivot;
    [SerializeField] private Transform lookAtTarget;
    [SerializeField] private bool flattenPivotYaw = true;
    [SerializeField] private Vector3 startOffset = new Vector3(-3.25f, 1.7f, -5.5f);
    [SerializeField] private Vector3 endOffset = new Vector3(0.85f, 1.75f, -1.2f);
    [SerializeField] private Vector3 lookAtOffset = Vector3.zero;

    [Header("Lens")]
    [SerializeField, Range(20f, 90f)] private float fieldOfView = 34f;

    [Header("Locks")]
    [SerializeField] private bool lockPlayerDuringIntro = true;
    [SerializeField] private FirstPersonController playerControllerOverride;
    [SerializeField] private PlayerActionLock playerActionLock;
    [SerializeField] private StarterAssetsInputs playerInputs;

    private Coroutine playRoutine;
    private bool hasPlayed;

    public bool IsPlaying => playRoutine != null;
    public float EstimatedDuration => Mathf.Max(0f, startDelay) + Mathf.Max(0.01f, duration) + Mathf.Max(0f, blendOutTime);

    private void Reset()
    {
        motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void Start()
    {
        if (!Application.isPlaying || !playOnStart)
        {
            return;
        }

        Play();
    }

    public void Play()
    {
        if (playRoutine != null)
        {
            return;
        }

        if (onlyOncePerSceneLoad && hasPlayed)
        {
            return;
        }

        playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        hasPlayed = true;

        FirstPersonController playerController = null;
        PlayerActionLock actionLock = null;
        StarterAssetsInputs inputs = null;
        CinemachineBrain brain = null;
        Transform shotPivot = null;
        Transform shotLookAt = null;
        CinemachineBlendDefinition originalBlend = default;
        bool lockAcquired = false;
        bool blendOverridden = false;
        GameObject introCameraObject = null;

        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        float resolveElapsed = 0f;
        while (!TryResolveSceneReferences(
            out playerController,
            out actionLock,
            out inputs,
            out brain,
            out shotPivot,
            out shotLookAt))
        {
            if (resolveElapsed >= resolveTargetTimeout)
            {
                Debug.LogWarning($"{nameof(EntranceCameraIntro)} on {name}: could not resolve player/camera references in scene '{gameObject.scene.name}'.", this);
                playRoutine = null;
                yield break;
            }

            resolveElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        try
        {
            ClearPlayerInputs(inputs);

            if (lockPlayerDuringIntro && actionLock != null)
            {
                actionLock.AcquireFullLock();
                lockAcquired = true;
            }

            introCameraObject = new GameObject("EntranceIntroCamera");
            CinemachineCamera introCamera = introCameraObject.AddComponent<CinemachineCamera>();
            introCamera.Priority = introPriority;

            LensSettings lens = introCamera.Lens;
            lens.FieldOfView = fieldOfView;
            introCamera.Lens = lens;

            // Force the camera to the very first pose immediately BEFORE the first frame
            ApplyShotPose(introCamera.transform, shotPivot, shotLookAt, 0f);
            introCamera.ForceCameraPosition(introCamera.transform.position, introCamera.transform.rotation);

            if (brain != null)
            {
                originalBlend = brain.DefaultBlend;
                brain.DefaultBlend = CreateBlendDefinition(blendInTime);
                blendOverridden = true;
            }

            yield return null;

            float shotDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            while (elapsed < shotDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / shotDuration);
                ApplyShotPose(introCamera.transform, shotPivot, shotLookAt, EvaluateMotion(normalizedTime));
                yield return null;
            }

            ApplyShotPose(introCamera.transform, shotPivot, shotLookAt, 1f);

            if (brain != null)
            {
                brain.DefaultBlend = CreateBlendDefinition(blendOutTime);
            }

            introCamera.Priority = -100;

            if (blendOutTime > 0f)
            {
                yield return new WaitForSecondsRealtime(blendOutTime);
            }
        }
        finally
        {
            if (brain != null && blendOverridden)
            {
                brain.DefaultBlend = originalBlend;
            }

            ClearPlayerInputs(inputs);

            if (lockAcquired && actionLock != null)
            {
                actionLock.ReleaseFullLock();
            }

            if (introCameraObject != null)
            {
                Destroy(introCameraObject);
            }

            playRoutine = null;
        }
    }

    private bool TryResolveSceneReferences(
        out FirstPersonController playerController,
        out PlayerActionLock actionLock,
        out StarterAssetsInputs inputs,
        out CinemachineBrain brain,
        out Transform shotPivot,
        out Transform shotLookAt)
    {
        playerController = playerControllerOverride != null
            ? playerControllerOverride
            : FindAnyObjectByType<FirstPersonController>();

        if (playerController == null)
        {
            actionLock = null;
            inputs = null;
            brain = null;
            shotPivot = null;
            shotLookAt = null;
            return false;
        }

        actionLock = playerActionLock != null
            ? playerActionLock
            : PlayerActionLock.GetOrCreate(playerController.gameObject);
        inputs = playerInputs != null
            ? playerInputs
            : playerController.GetComponent<StarterAssetsInputs>();

        Camera mainCamera = Camera.main;
        brain = mainCamera != null
            ? mainCamera.GetComponent<CinemachineBrain>()
            : null;

        if (brain == null)
        {
            brain = FindAnyObjectByType<CinemachineBrain>();
        }

        shotPivot = pivot != null ? pivot : playerController.transform;
        shotLookAt = lookAtTarget != null
            ? lookAtTarget
            : playerController.CinemachineCameraTarget != null
                ? playerController.CinemachineCameraTarget.transform
                : playerController.transform;

        return brain != null && shotPivot != null && shotLookAt != null;
    }

    private void ApplyShotPose(Transform introCameraTransform, Transform shotPivot, Transform shotLookAt, float t)
    {
        if (introCameraTransform == null || shotPivot == null)
        {
            return;
        }

        Vector3 currentStartOffset = startOffset;
        Vector3 currentEndOffset = endOffset;
        Vector3 currentLookAtOffset = lookAtOffset;
        float positionT = t;

        if (preset == IntroPreset.GetOutOfCar)
        {
            currentStartOffset = new Vector3(0f, 2.4f, 0f); 
            currentEndOffset = new Vector3(0f, 1.6f, 0f);     // End at player head height
            
            // Drop much faster: hit the ground at t=0.35. Accelerate downwards (gravity) using t*t
            float dropProgress = Mathf.Clamp01(t / 0.35f);
            positionT = dropProgress * dropProgress;
        }

        Quaternion shotRotation = flattenPivotYaw
            ? Quaternion.Euler(0f, shotPivot.eulerAngles.y, 0f)
            : shotPivot.rotation;

        Vector3 worldStart = shotPivot.position + shotRotation * currentStartOffset;
        Vector3 worldEnd = shotPivot.position + shotRotation * currentEndOffset;
        Vector3 worldPosition = Vector3.Lerp(worldStart, worldEnd, Mathf.Clamp01(positionT));

        // Add procedural bounce for GetOutOfCar instantly upon impact (t >= 0.35)
        // (Bounce removed to fix unwanted vibration)


        Vector3 lookTargetPosition = shotLookAt != null
            ? shotLookAt.TransformPoint(currentLookAtOffset)
            : shotPivot.position + shotRotation * currentLookAtOffset;

        if (preset == IntroPreset.GetOutOfCar)
        {
            float dropProgress = Mathf.Clamp01(t / 0.35f);
            
            // Look slightly down at start to see the ground rushing up, then look straight ahead at landing
            Vector3 startLookTarget = shotPivot.position + shotRotation * new Vector3(0f, 1.0f, 4f);
            Vector3 endLookTarget = shotPivot.position + shotRotation * new Vector3(0f, 1.6f, 5f);
            
            lookTargetPosition = Vector3.Lerp(startLookTarget, endLookTarget, dropProgress);
        }
        
        Vector3 forward = lookTargetPosition - worldPosition;
        Quaternion worldRotation = forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward.normalized, Vector3.up)
            : introCameraTransform.rotation;

        // Add procedural tilt/rotation shake for GetOutOfCar instantly upon impact
        if (preset == IntroPreset.GetOutOfCar && t >= 0.35f)
        {
            float shakeProgress = (t - 0.35f) / 0.65f; // 0 to 1 over the remaining time
            
            // Single-hit impact: Left (roll -) then Right (roll +)
            // t = 0.35: Left (-4.0), t = 0.5: Right (+2.0), t = 1.0: Center (0)
            float rollTilt = 0f;
            if (shakeProgress < 0.2f) {
                // Initial hit to left
                rollTilt = Mathf.Lerp(0f, -4.0f, shakeProgress * 5f);
            } else if (shakeProgress < 0.5f) {
                // Kick to right
                float t2 = (shakeProgress - 0.2f) / 0.3f;
                rollTilt = Mathf.Lerp(-4.0f, 2.5f, t2);
            } else {
                // Settle back to center
                float t3 = (shakeProgress - 0.5f) / 0.5f;
                rollTilt = Mathf.Lerp(2.5f, 0f, t3);
            }
            
            worldRotation *= Quaternion.Euler(0f, 0f, rollTilt);
        }

        introCameraTransform.SetPositionAndRotation(worldPosition, worldRotation);
    }

    private float EvaluateMotion(float normalizedTime)
    {
        if (motionCurve == null || motionCurve.length == 0)
        {
            return normalizedTime;
        }

        return Mathf.Clamp01(motionCurve.Evaluate(normalizedTime));
    }

    private static CinemachineBlendDefinition CreateBlendDefinition(float blendTime)
    {
        return blendTime <= 0f
            ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
            : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendTime);
    }

    private static void ClearPlayerInputs(StarterAssetsInputs inputs)
    {
        if (inputs == null)
        {
            return;
        }

        inputs.MoveInput(Vector2.zero);
        inputs.LookInput(Vector2.zero);
        inputs.JumpInput(false);
        inputs.SprintInput(false);
        inputs.CrouchInput(false);
        inputs.InteractInput(false);
        inputs.PickupInput(false);
        inputs.UseInput(false);
        inputs.DropInput(false);
        inputs.GrabInput(false);
        inputs.ClearGameplayActionInputs();
    }
}
