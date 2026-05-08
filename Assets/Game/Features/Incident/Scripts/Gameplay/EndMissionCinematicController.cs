using System;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class EndMissionCinematicController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EndCinematicCameraPath cameraPath;
    [SerializeField] private Transform lookAtTarget;
    [SerializeField] private FirstPersonController playerControllerOverride;
    [SerializeField] private PlayerActionLock playerActionLock;
    [SerializeField] private StarterAssetsInputs playerInputs;
    [SerializeField] private CinemachineBrain cinemachineBrain;
    [SerializeField] private Transform runtimeCameraParent;

    [Header("Flow")]
    [SerializeField] private bool playBeforeResultOverlay = true;
    [SerializeField] private bool lockPlayerDuringCinematic = true;
    [SerializeField] private bool keepCinematicCameraAfterComplete = true;
    [SerializeField] private bool allowDirectMainCameraFallback = true;
    [SerializeField] private bool disableOtherCinemachineCamerasDuringShot = true;
    [SerializeField] private float startDelay = 0.25f;
    [SerializeField] private float blendInTime = 0.9f;
    [SerializeField] private float blendOutTime = 0.45f;
    [SerializeField] private int cameraPriority = 110;
    [SerializeField] private AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Motion Feel")]
    [SerializeField] private bool useSmoothPathInterpolation = true;
    [SerializeField, Range(0f, 6f)] private float pathBankAngle = 2f;

    [Header("Loop")]
    [SerializeField] private bool loopPathAfterHold;
    [SerializeField] private float loopDuration = 12f;

    [Header("Fallback Shot")]
    [SerializeField] private float fallbackDuration = 6.5f;
    [SerializeField] private float fallbackHoldDuration = 1.2f;
    [SerializeField] private float fallbackRiseHeight = 24f;
    [SerializeField] private float fallbackPullbackDistance = 34f;
    [SerializeField] private float fallbackSideOffset = 6f;
    [SerializeField] private float fallbackLookAtHeightOffset = 1.5f;
    [SerializeField, Range(20f, 90f)] private float fallbackFieldOfView = 38f;

    [Header("Events")]
    [SerializeField] private UnityEvent onCinematicStarted;
    [SerializeField] private UnityEvent onCinematicFinished;

    private readonly List<Vector3> pathPositions = new List<Vector3>();
    private readonly List<Quaternion> pathRotations = new List<Quaternion>();
    private readonly List<CinemachineCamera> suppressedCinemachineCameras = new List<CinemachineCamera>();
    private readonly List<bool> suppressedCameraEnabledStates = new List<bool>();

    private Coroutine playRoutine;
    private Action pendingCompletion;
    private GameObject runtimeCameraObject;
    private CinemachineCamera runtimeCamera;
    private CinemachineBlendDefinition originalBlend;
    private PlayerActionLock activeActionLock;
    private CinemachineBrain activeBrain;
    private Transform directCameraTransform;
    private bool usingDirectMainCameraFallback;
    private bool blendOverridden;
    private bool lockAcquired;
    private bool useBuiltPathWaypointRotations;
    private float[] segmentDurations = Array.Empty<float>();
    private float totalPathDuration;

    public bool IsPlaying => playRoutine != null;
    public bool CanPlayBeforeResultOverlay => playBeforeResultOverlay && isActiveAndEnabled;

    private void Reset()
    {
        motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void OnDisable()
    {
        StopCinematic(completePendingCallback: true);
    }

    private void OnDestroy()
    {
        StopCinematic(completePendingCallback: true);
    }

    public bool TryPlayBeforeResult(Action onCompleted)
    {
        if (!CanPlayBeforeResultOverlay)
        {
            return false;
        }

        return TryPlay(onCompleted);
    }

    private bool TryPlay(Action onCompleted)
    {
        pendingCompletion += onCompleted;
        if (playRoutine != null)
        {
            return true;
        }

        playRoutine = StartCoroutine(PlayRoutine());
        return true;
    }

    public void StopCinematic()
    {
        StopCinematic(completePendingCallback: false);
    }

    private IEnumerator PlayRoutine()
    {
        onCinematicStarted?.Invoke();

        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        if (!TryResolveSceneReferences(
                out FirstPersonController playerController,
                out PlayerActionLock actionLock,
                out StarterAssetsInputs inputs,
                out CinemachineBrain brain,
                out Camera directCamera))
        {
            Debug.LogWarning($"{nameof(EndMissionCinematicController)} on {name}: could not resolve camera control for end mission cinematic.", this);
            FinishCinematic(invokeCallback: true);
            yield break;
        }

        try
        {
            ClearPlayerInputs(inputs);

            if (lockPlayerDuringCinematic && actionLock != null)
            {
                activeActionLock = actionLock;
                actionLock.AcquireFullLock();
                lockAcquired = true;
            }

            Vector3 lookPosition = ResolveLookPosition(playerController);
            BuildPath(playerController, lookPosition);
            if (pathPositions.Count < 2)
            {
                FinishCinematic(invokeCallback: true);
                yield break;
            }

            RebuildSegmentDurations();

            usingDirectMainCameraFallback = brain == null && directCamera != null;
            directCameraTransform = usingDirectMainCameraFallback ? directCamera.transform : null;

            if (usingDirectMainCameraFallback)
            {
                directCamera.fieldOfView = ResolveFieldOfView();
            }
            else
            {
                runtimeCameraObject = new GameObject("EndMissionCinematicCamera");
                Transform parent = ResolveRuntimeCameraParent(brain);
                if (parent != null)
                {
                    runtimeCameraObject.transform.SetParent(parent, worldPositionStays: true);
                }

                runtimeCamera = runtimeCameraObject.AddComponent<CinemachineCamera>();
                runtimeCamera.Priority = ResolveRuntimeCameraPriority();

                LensSettings lens = runtimeCamera.Lens;
                lens.FieldOfView = ResolveFieldOfView();
                runtimeCamera.Lens = lens;
                SuppressOtherCinemachineCameras();
            }

            ApplyCameraPose(0f, lookPosition);
            if (runtimeCamera != null)
            {
                runtimeCamera.ForceCameraPosition(
                    runtimeCamera.transform.position,
                    runtimeCamera.transform.rotation);
            }

            if (brain != null)
            {
                originalBlend = brain.DefaultBlend;
                brain.DefaultBlend = CreateBlendDefinition(blendInTime);
                activeBrain = brain;
                blendOverridden = true;
            }

            yield return null;

            float shotDuration = Mathf.Max(0.01f, totalPathDuration);
            float elapsed = 0f;
            while (elapsed < shotDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / shotDuration);
                float curvedElapsed = EvaluateMotion(normalizedTime) * shotDuration;
                ApplyCameraPose(curvedElapsed, lookPosition);
                yield return null;
            }

            ApplyCameraPose(shotDuration, lookPosition);

            float holdDuration = ResolveHoldDuration();
            if (holdDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(holdDuration);
            }

            if (ShouldLoopPathAfterHold())
            {
                InvokeAndClearPendingCompletion();
                ReleasePlayerLock(actionLock);
                RestoreBrainBlend(brain);
                yield return LoopPathRoutine(lookPosition);
            }
        }
        finally
        {
            ClearPlayerInputs(inputs);
            ReleasePlayerLock(actionLock);
            RestoreBrainBlend(brain);
        }

        if (!keepCinematicCameraAfterComplete && !usingDirectMainCameraFallback)
        {
            ReleaseRuntimeCamera();
            if (blendOutTime > 0f)
            {
                yield return new WaitForSecondsRealtime(blendOutTime);
            }
        }

        FinishCinematic(invokeCallback: true);
    }

    private IEnumerator LoopPathRoutine(Vector3 lookPosition)
    {
        float duration = Mathf.Max(0.01f, loopDuration);
        float startNormalizedTime = pathPositions.Count > 1
            ? (pathPositions.Count - 1f) / pathPositions.Count
            : 0f;
        float elapsed = startNormalizedTime * duration;

        while (true)
        {
            elapsed = Mathf.Repeat(elapsed + Time.unscaledDeltaTime, duration);
            ApplyLoopCameraPose(elapsed / duration, lookPosition);
            yield return null;
        }
    }

    private bool TryResolveSceneReferences(
        out FirstPersonController playerController,
        out PlayerActionLock actionLock,
        out StarterAssetsInputs inputs,
        out CinemachineBrain brain,
        out Camera directCamera)
    {
        playerController = playerControllerOverride != null
            ? playerControllerOverride
            : FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Exclude);

        actionLock = playerActionLock;
        inputs = playerInputs;
        brain = cinemachineBrain;
        directCamera = Camera.main;

        if (playerController != null)
        {
            if (actionLock == null)
            {
                actionLock = PlayerActionLock.GetOrCreate(playerController.gameObject);
            }

            if (inputs == null)
            {
                inputs = playerController.GetComponent<StarterAssetsInputs>();
            }
        }

        if (brain == null)
        {
            Camera mainCamera = Camera.main;
            brain = mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null;
            directCamera = mainCamera;
        }

        if (brain == null)
        {
            brain = FindAnyObjectByType<CinemachineBrain>(FindObjectsInactive.Exclude);
        }

        if (brain != null)
        {
            return true;
        }

        if (!allowDirectMainCameraFallback)
        {
            return false;
        }

        if (directCamera == null)
        {
            directCamera = FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude);
        }

        return directCamera != null;
    }

    private void BuildPath(FirstPersonController playerController, Vector3 lookPosition)
    {
        pathPositions.Clear();
        pathRotations.Clear();
        useBuiltPathWaypointRotations = false;

        EndCinematicCameraPath resolvedPath = ResolveCameraPath();
        if (resolvedPath != null && resolvedPath.ValidWaypointCount > 0)
        {
            BuildWaypointPath(resolvedPath, lookPosition);
            return;
        }

        BuildFallbackPath(playerController, lookPosition);
    }

    private void BuildWaypointPath(EndCinematicCameraPath resolvedPath, Vector3 lookPosition)
    {
        useBuiltPathWaypointRotations = resolvedPath.UseWaypointRotations;
        int count = resolvedPath.ValidWaypointCount;
        for (int i = 0; i < count; i++)
        {
            if (!resolvedPath.TryGetWaypoint(i, out Transform waypoint) || waypoint == null)
            {
                continue;
            }

            pathPositions.Add(waypoint.position);
            pathRotations.Add(resolvedPath.UseWaypointRotations ? waypoint.rotation : LookAt(pathPositions[pathPositions.Count - 1], lookPosition));
        }

        if (pathPositions.Count == 1)
        {
            Vector3 currentPosition = ResolveCurrentCameraPosition(pathPositions[0]);
            pathPositions.Insert(0, currentPosition);
            pathRotations.Insert(0, LookAt(currentPosition, lookPosition));
        }
    }

    private void BuildFallbackPath(FirstPersonController playerController, Vector3 lookPosition)
    {
        Vector3 startPosition = ResolveCurrentCameraPosition(ResolvePlayerPosition(playerController, lookPosition));
        Vector3 away = Vector3.ProjectOnPlane(startPosition - lookPosition, Vector3.up);
        if (away.sqrMagnitude < 0.0001f && playerController != null)
        {
            away = -Vector3.ProjectOnPlane(playerController.transform.forward, Vector3.up);
        }

        if (away.sqrMagnitude < 0.0001f)
        {
            away = Vector3.back;
        }

        Vector3 awayDirection = away.normalized;
        Vector3 sideDirection = Vector3.Cross(Vector3.up, awayDirection).normalized;
        Vector3 elevatedPosition = startPosition + Vector3.up * (fallbackRiseHeight * 0.55f);
        Vector3 revealPosition =
            lookPosition
            + awayDirection * Mathf.Max(1f, fallbackPullbackDistance)
            + sideDirection * fallbackSideOffset
            + Vector3.up * Mathf.Max(1f, fallbackRiseHeight);

        pathPositions.Add(startPosition);
        pathPositions.Add(elevatedPosition);
        pathPositions.Add(revealPosition);

        for (int i = 0; i < pathPositions.Count; i++)
        {
            pathRotations.Add(LookAt(pathPositions[i], lookPosition));
        }
    }

    private EndCinematicCameraPath ResolveCameraPath()
    {
        if (cameraPath != null)
        {
            return cameraPath;
        }

        cameraPath = FindAnyObjectByType<EndCinematicCameraPath>(FindObjectsInactive.Exclude);
        return cameraPath;
    }

    private Vector3 ResolveLookPosition(FirstPersonController playerController)
    {
        Transform target = lookAtTarget != null ? lookAtTarget : ResolveCameraPath()?.LookAtTarget;
        if (target != null)
        {
            return target.position;
        }

        if (playerController != null)
        {
            Transform cameraTarget = playerController.CinemachineCameraTarget != null
                ? playerController.CinemachineCameraTarget.transform
                : playerController.transform;
            return cameraTarget.position + Vector3.up * fallbackLookAtHeightOffset;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera.transform.position + mainCamera.transform.forward * 8f;
        }

        return transform.position + Vector3.up * fallbackLookAtHeightOffset;
    }

    private Vector3 ResolveCurrentCameraPosition(Vector3 fallbackPosition)
    {
        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform.position : fallbackPosition;
    }

    private Vector3 ResolvePlayerPosition(FirstPersonController playerController, Vector3 fallbackPosition)
    {
        return playerController != null ? playerController.transform.position : fallbackPosition;
    }

    private bool ShouldLoopPathAfterHold()
    {
        return loopPathAfterHold
            && keepCinematicCameraAfterComplete
            && pathPositions.Count >= 2;
    }

    private Transform ResolveRuntimeCameraParent(CinemachineBrain brain)
    {
        if (runtimeCameraParent != null)
        {
            return runtimeCameraParent;
        }

        Transform namedCameraContainer = FindNamedCameraContainer();
        if (namedCameraContainer != null)
        {
            return namedCameraContainer;
        }

        return brain != null && brain.transform.parent != null
            ? brain.transform.parent
            : null;
    }

    private static Transform FindNamedCameraContainer()
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        Transform fallback = null;
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != "Camera")
            {
                continue;
            }

            if (HasCameraDescendant(candidate))
            {
                return candidate;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }
        }

        return fallback;
    }

    private static bool HasCameraDescendant(Transform candidate)
    {
        return candidate.GetComponentInChildren<Camera>(true) != null
            || candidate.GetComponentInChildren<CinemachineCamera>(true) != null;
    }

    private void ReleasePlayerLock(PlayerActionLock actionLock)
    {
        if (!lockAcquired || actionLock == null)
        {
            return;
        }

        actionLock.ReleaseFullLock();
        lockAcquired = false;
        activeActionLock = null;
    }

    private void RestoreBrainBlend(CinemachineBrain brain)
    {
        if (brain == null || !blendOverridden)
        {
            return;
        }

        brain.DefaultBlend = originalBlend;
        blendOverridden = false;
        activeBrain = null;
    }

    private void ApplyCameraPose(float elapsedTime, Vector3 lookPosition)
    {
        Transform cameraTransform = runtimeCamera != null
            ? runtimeCamera.transform
            : directCameraTransform;
        if (cameraTransform == null || pathPositions.Count == 0)
        {
            return;
        }

        Vector3 position = useSmoothPathInterpolation
            ? EvaluateSmoothPathPosition(elapsedTime)
            : EvaluateLinearPathPosition(elapsedTime);
        Quaternion rotation = useBuiltPathWaypointRotations
            ? EvaluatePathRotation(elapsedTime, lookPosition)
            : BuildNaturalLookRotation(elapsedTime, position, lookPosition);

        cameraTransform.SetPositionAndRotation(position, rotation);
    }

    private void ApplyLoopCameraPose(float normalizedTime, Vector3 lookPosition)
    {
        Transform cameraTransform = runtimeCamera != null
            ? runtimeCamera.transform
            : directCameraTransform;
        if (cameraTransform == null || pathPositions.Count == 0)
        {
            return;
        }

        Vector3 position = EvaluateLoopPathPosition(normalizedTime);
        Quaternion rotation = useBuiltPathWaypointRotations
            ? EvaluateLoopPathRotation(normalizedTime, lookPosition)
            : BuildLoopLookRotation(normalizedTime, position, lookPosition);

        cameraTransform.SetPositionAndRotation(position, rotation);
    }

    private Vector3 EvaluateLinearPathPosition(float elapsedTime)
    {
        if (!TryResolveSegmentSample(elapsedTime, out int segmentIndex, out float segmentTime))
        {
            return pathPositions.Count > 0 ? pathPositions[pathPositions.Count - 1] : transform.position;
        }

        return Vector3.Lerp(pathPositions[segmentIndex], pathPositions[segmentIndex + 1], segmentTime);
    }

    private Vector3 EvaluateSmoothPathPosition(float elapsedTime)
    {
        if (!TryResolveSegmentSample(elapsedTime, out int segmentIndex, out float segmentTime))
        {
            return pathPositions.Count > 0 ? pathPositions[pathPositions.Count - 1] : transform.position;
        }

        int lastIndex = pathPositions.Count - 1;

        Vector3 p0 = pathPositions[Mathf.Max(segmentIndex - 1, 0)];
        Vector3 p1 = pathPositions[segmentIndex];
        Vector3 p2 = pathPositions[segmentIndex + 1];
        Vector3 p3 = pathPositions[Mathf.Min(segmentIndex + 2, lastIndex)];
        return CatmullRom(p0, p1, p2, p3, segmentTime);
    }

    private Vector3 EvaluateLoopPathPosition(float normalizedTime)
    {
        int count = pathPositions.Count;
        if (count <= 1)
        {
            return count == 1 ? pathPositions[0] : transform.position;
        }

        float scaledTime = Mathf.Repeat(normalizedTime, 1f) * count;
        int segmentIndex = Mathf.FloorToInt(scaledTime) % count;
        float segmentTime = scaledTime - Mathf.Floor(scaledTime);

        Vector3 p0 = pathPositions[WrapIndex(segmentIndex - 1, count)];
        Vector3 p1 = pathPositions[segmentIndex];
        Vector3 p2 = pathPositions[WrapIndex(segmentIndex + 1, count)];
        Vector3 p3 = pathPositions[WrapIndex(segmentIndex + 2, count)];
        return CatmullRom(p0, p1, p2, p3, segmentTime);
    }

    private Quaternion EvaluatePathRotation(float elapsedTime, Vector3 lookPosition)
    {
        if (pathRotations.Count <= 0)
        {
            return pathRotations.Count > 0 ? pathRotations[0] : LookAt(transform.position, lookPosition);
        }

        if (!TryResolveSegmentSample(elapsedTime, out int segmentIndex, out float segmentTime))
        {
            return pathRotations[pathRotations.Count - 1];
        }

        return Quaternion.Slerp(pathRotations[segmentIndex], pathRotations[segmentIndex + 1], segmentTime);
    }

    private Quaternion EvaluateLoopPathRotation(float normalizedTime, Vector3 lookPosition)
    {
        int count = pathRotations.Count;
        if (count <= 1)
        {
            return count == 1 ? pathRotations[0] : LookAt(transform.position, lookPosition);
        }

        float scaledTime = Mathf.Repeat(normalizedTime, 1f) * count;
        int segmentIndex = Mathf.FloorToInt(scaledTime) % count;
        int nextIndex = WrapIndex(segmentIndex + 1, count);
        float segmentTime = Mathf.SmoothStep(0f, 1f, scaledTime - Mathf.Floor(scaledTime));

        return Quaternion.Slerp(pathRotations[segmentIndex], pathRotations[nextIndex], segmentTime);
    }

    private Quaternion BuildNaturalLookRotation(float elapsedTime, Vector3 position, Vector3 lookPosition)
    {
        Quaternion rotation = LookAt(position, lookPosition);
        if (pathBankAngle <= 0f || pathPositions.Count < 2)
        {
            return rotation;
        }

        const float sampleOffset = 0.08f;
        Vector3 previous = useSmoothPathInterpolation
            ? EvaluateSmoothPathPosition(Mathf.Max(0f, elapsedTime - sampleOffset))
            : EvaluateLinearPathPosition(Mathf.Max(0f, elapsedTime - sampleOffset));
        Vector3 next = useSmoothPathInterpolation
            ? EvaluateSmoothPathPosition(Mathf.Min(totalPathDuration, elapsedTime + sampleOffset))
            : EvaluateLinearPathPosition(Mathf.Min(totalPathDuration, elapsedTime + sampleOffset));
        Vector3 movement = next - previous;
        if (movement.sqrMagnitude < 0.0001f)
        {
            return rotation;
        }

        Vector3 right = rotation * Vector3.right;
        float lateralMovement = Vector3.Dot(movement.normalized, right);
        float roll = Mathf.Clamp(-lateralMovement * pathBankAngle, -pathBankAngle, pathBankAngle);
        return rotation * Quaternion.Euler(0f, 0f, roll);
    }

    private Quaternion BuildLoopLookRotation(float normalizedTime, Vector3 position, Vector3 lookPosition)
    {
        Quaternion rotation = LookAt(position, lookPosition);
        if (pathBankAngle <= 0f || pathPositions.Count < 2)
        {
            return rotation;
        }

        const float sampleOffset = 0.025f;
        Vector3 previous = EvaluateLoopPathPosition(normalizedTime - sampleOffset);
        Vector3 next = EvaluateLoopPathPosition(normalizedTime + sampleOffset);
        Vector3 movement = next - previous;
        if (movement.sqrMagnitude < 0.0001f)
        {
            return rotation;
        }

        Vector3 right = rotation * Vector3.right;
        float lateralMovement = Vector3.Dot(movement.normalized, right);
        float roll = Mathf.Clamp(-lateralMovement * pathBankAngle, -pathBankAngle, pathBankAngle);
        return rotation * Quaternion.Euler(0f, 0f, roll);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static int WrapIndex(int index, int count)
    {
        return (index % count + count) % count;
    }

    private void RebuildSegmentDurations()
    {
        int segmentCount = Mathf.Max(0, pathPositions.Count - 1);
        if (segmentCount <= 0)
        {
            segmentDurations = Array.Empty<float>();
            totalPathDuration = 0f;
            return;
        }

        segmentDurations = new float[segmentCount];
        totalPathDuration = 0f;
        float speed = ResolveSpeed();
        for (int i = 0; i < segmentCount; i++)
        {
            float distance = Vector3.Distance(pathPositions[i], pathPositions[i + 1]);
            float duration = distance / Mathf.Max(0.01f, speed);
            segmentDurations[i] = Mathf.Max(0.01f, duration);
            totalPathDuration += segmentDurations[i];
        }
    }

    private float ResolveHoldDuration()
    {
        EndCinematicCameraPath resolvedPath = ResolveCameraPath();
        return resolvedPath != null && resolvedPath.ValidWaypointCount > 0
            ? resolvedPath.HoldDuration
            : Mathf.Max(0f, fallbackHoldDuration);
    }

    private float ResolveFieldOfView()
    {
        EndCinematicCameraPath resolvedPath = ResolveCameraPath();
        return resolvedPath != null && resolvedPath.ValidWaypointCount > 0
            ? resolvedPath.FieldOfView
            : Mathf.Clamp(fallbackFieldOfView, 20f, 90f);
    }

    private float ResolveSpeed()
    {
        EndCinematicCameraPath resolvedPath = ResolveCameraPath();
        return resolvedPath != null && resolvedPath.ValidWaypointCount > 0
            ? resolvedPath.Speed
            : Mathf.Max(0.01f, fallbackPullbackDistance / Mathf.Max(0.01f, fallbackDuration));
    }

    private float EvaluateMotion(float normalizedTime)
    {
        if (motionCurve == null || motionCurve.length == 0)
        {
            return normalizedTime;
        }

        return Mathf.Clamp01(motionCurve.Evaluate(normalizedTime));
    }

    private static Quaternion LookAt(Vector3 position, Vector3 lookPosition)
    {
        Vector3 forward = lookPosition - position;
        return forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward.normalized, Vector3.up)
            : Quaternion.identity;
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

    private void ReleaseRuntimeCamera()
    {
        if (runtimeCamera != null)
        {
            runtimeCamera.Priority = -100;
        }

        if (runtimeCameraObject != null)
        {
            Destroy(runtimeCameraObject);
        }

        runtimeCamera = null;
        runtimeCameraObject = null;
    }

    private int ResolveRuntimeCameraPriority()
    {
        int resolvedPriority = Mathf.Max(100, cameraPriority);
        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);
        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera candidate = cameras[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.Priority >= resolvedPriority)
            {
                resolvedPriority = candidate.Priority + 1;
            }
        }

        return resolvedPriority;
    }

    private void SuppressOtherCinemachineCameras()
    {
        RestoreSuppressedCinemachineCameras();
        if (!disableOtherCinemachineCamerasDuringShot || runtimeCamera == null)
        {
            return;
        }

        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);
        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera candidate = cameras[i];
            if (candidate == null || candidate == runtimeCamera)
            {
                continue;
            }

            suppressedCinemachineCameras.Add(candidate);
            suppressedCameraEnabledStates.Add(candidate.enabled);
            if (candidate.enabled)
            {
                candidate.enabled = false;
            }
        }
    }

    private void RestoreSuppressedCinemachineCameras()
    {
        int count = Mathf.Min(
            suppressedCinemachineCameras.Count,
            suppressedCameraEnabledStates.Count);
        for (int i = 0; i < count; i++)
        {
            CinemachineCamera candidate = suppressedCinemachineCameras[i];
            if (candidate == null)
            {
                continue;
            }

            candidate.enabled = suppressedCameraEnabledStates[i];
        }

        suppressedCinemachineCameras.Clear();
        suppressedCameraEnabledStates.Clear();
    }

    private void StopCinematic(bool completePendingCallback)
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (lockAcquired && activeActionLock != null)
        {
            activeActionLock.ReleaseFullLock();
            lockAcquired = false;
            activeActionLock = null;
        }

        if (activeBrain != null && blendOverridden)
        {
            activeBrain.DefaultBlend = originalBlend;
            blendOverridden = false;
            activeBrain = null;
        }

        ReleaseRuntimeCamera();
        RestoreSuppressedCinemachineCameras();
        directCameraTransform = null;
        usingDirectMainCameraFallback = false;

        if (completePendingCallback)
        {
            InvokeAndClearPendingCompletion();
        }
        else
        {
            pendingCompletion = null;
        }
    }

    private void FinishCinematic(bool invokeCallback)
    {
        playRoutine = null;
        onCinematicFinished?.Invoke();

        if (invokeCallback)
        {
            InvokeAndClearPendingCompletion();
        }
    }

    private void InvokeAndClearPendingCompletion()
    {
        Action callback = pendingCompletion;
        pendingCompletion = null;
        callback?.Invoke();
    }

    private bool TryResolveSegmentSample(float elapsedTime, out int segmentIndex, out float segmentTime)
    {
        segmentIndex = 0;
        segmentTime = 0f;

        int segmentCount = segmentDurations != null ? segmentDurations.Length : 0;
        if (segmentCount <= 0 || pathPositions.Count < 2)
        {
            return false;
        }

        float remainingTime = Mathf.Clamp(elapsedTime, 0f, totalPathDuration);
        for (int i = 0; i < segmentCount; i++)
        {
            float duration = Mathf.Max(0.01f, segmentDurations[i]);
            if (remainingTime <= duration || i == segmentCount - 1)
            {
                segmentIndex = i;
                segmentTime = Mathf.Clamp01(remainingTime / duration);
                return true;
            }

            remainingTime -= duration;
        }

        segmentIndex = segmentCount - 1;
        segmentTime = 1f;
        return true;
    }
}
