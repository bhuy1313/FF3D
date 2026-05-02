using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using TrueJourney.BotBehavior;
using System.Collections.Generic;

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FPSInteractionSystem : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float interactDistance = 3f;
        [SerializeField] private LayerMask interactMask = ~0;
        [SerializeField] private bool drawDebugRay;
        [SerializeField] private bool enableOutlineHighlight = true;
        [SerializeField, Range(0, 31)] private int outlineRenderingLayer = 6;

        // Input
        [SerializeField] private StarterAssetsInputs input;

        [Header("Grab")]
        [SerializeField] private LayerMask grabMask = ~0;
        [SerializeField] private float grabDistance = 3f;
        [SerializeField] private float maxGrabMass = 50f;
        [SerializeField] private Transform grabPoint;
        [SerializeField] private Vector3 grabPointLocalPosition = Vector3.zero;
        [SerializeField] private bool enableGrabPlacement = true;
        [SerializeField] private LayerMask grabPlacementMask = ~0;
        [SerializeField] private float grabPlacementDistance = 4f;
        [SerializeField] private float grabPlacementSurfacePadding = 0.02f;
        [SerializeField] private Color grabPlacementPreviewColor = new Color(0.45f, 0.95f, 0.65f, 0.4f);

        [Header("References")]
        [SerializeField] private FPSInventorySystem inventory;

        private IInteractable currentInteractable;
        [SerializeField] private GameObject currentTarget;

        public GameObject CurrentTarget => currentTarget;
        public float InteractDistance => interactDistance;
        public bool IsGrabActive => grabbedBody != null;
        public GameObject GrabbedObject => grabbedBody != null ? grabbedBody.gameObject : null;
        public GameObject CurrentHandOccupyingObject => GrabbedObject != null
            ? GrabbedObject
            : inventory != null
                ? inventory.HeldObject
                : null;
        public bool AreHandsOccupied => HandOccupancyUtility.IsHandsOccupied(CurrentHandOccupyingObject, gameObject);
        public float CurrentGrabWeightKg => ResolveGrabbedBodyWeightKg(grabbedBody);
        public float CurrentCarryWeightKg => ResolveRescuableCarryWeightKg(cachedCarriedRescuable);
        public float CurrentMovementBurdenKg => CurrentGrabWeightKg + CurrentCarryWeightKg;
        public bool IsCarryingVictim => isCarryingVictim;

        private Rigidbody grabbedBody;
        private Transform grabbedOriginalParent;
        private bool grabbedOriginalIsKinematic;
        private bool grabbedOriginalDetectCollisions;
        private bool previousInteract;
        private bool previousPickup;
        private bool previousUse;
        private bool previousDrop;
        private bool previousGrab;
        private GameObject outlinedTargetRoot;
        private Renderer[] outlinedRenderers;
        private bool[] outlinedRendererHadBit;
        private PlayerActionLock playerActionLock;
        private PlayerVitals playerVitals;
        private GameObject grabbedPreviewRoot;
        private Material grabbedPreviewMaterial;
        private ICustomGrabPlacement grabbedPlacementOverride;
        private readonly List<MonoBehaviour> grabbedBurnableSources = new List<MonoBehaviour>();
        private bool hasLoggedLegacyGrabFireWarning;
        private IRescuableTarget cachedCarriedRescuable;
        private bool isCarryingVictim;

        private void Awake()
        {
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            if (inventory == null)
            {
                inventory = GetComponent<FPSInventorySystem>();
            }

            if (input == null)
            {
                input = GetComponent<StarterAssetsInputs>();
            }

            playerActionLock = GetComponent<PlayerActionLock>();
            playerVitals = GetComponent<PlayerVitals>();

            grabDistance = interactDistance;
            ResolveGrabPoint();
        }

        private void Update()
        {
            if (viewCamera == null)
            {
                return;
            }

            cachedCarriedRescuable = FindPlayerCarriedRescuable();
            isCarryingVictim = cachedCarriedRescuable != null;

            if (IsGrabActive)
            {
                UpdateFocus(grabbedBody != null ? grabbedBody.transform : null);
            }
            else
            {
                UpdateFocus(null);
            }

            bool currentGrab = input != null && input.grab;
            bool grabPressed = WasPressed(currentGrab, ref previousGrab);

            if (grabPressed && !IsCarryRestricted())
            {
                ToggleGrab();
            }

            if (IsGrabActive)
            {
                bool usePressedWhileGrabbed = WasPressed(input != null && input.use, ref previousUse);
                bool interactPressedWhileGrabbed = WasPressed(input != null && input.interact, ref previousInteract);
                WasPressed(input != null && input.pickup, ref previousPickup);
                WasPressed(input != null && input.drop, ref previousDrop);

                ApplyHeldBurnDamage();
                UpdateGrabPlacementPreview();

                if (usePressedWhileGrabbed)
                {
                    TryPlaceGrabbed();
                }

                if (interactPressedWhileGrabbed && CanInteractWhileGrabbed(currentInteractable))
                {
                    currentInteractable.Interact(gameObject);
                    NotifyInteractionSignalRelay(currentTarget, currentInteractable, gameObject);
                }

                BlockGameplayActionsWhileGrabbed();
                return;
            }

            if (Input.GetKeyDown(KeyCode.V))
            {
                if (TryStartWindowClimbAtCurrentTarget())
                {
                    return;
                }
            }

            bool pickupPressed = WasPressed(input != null && input.pickup, ref previousPickup);
            if (pickupPressed && inventory != null)
            {
                if (IsInventoryActionBlockedByCarry())
                {
                    return;
                }

                if (currentTarget != null)
                {
                    inventory.TryPickup(currentTarget, gameObject);
                    return;
                }
            }

            if (WasPressed(input != null && input.interact, ref previousInteract))
            {
                if (TryCompleteCarriedRescueAtCurrentTarget())
                {
                    return;
                }

                if (currentInteractable != null && !IsGeneralInteractionBlockedByCarry(currentInteractable))
                {
                    currentInteractable.Interact(gameObject);
                    NotifyInteractionSignalRelay(currentTarget, currentInteractable, gameObject);
                }
            }

            if (WasPressed(input != null && input.use, ref previousUse) && inventory != null && inventory.HasItem)
            {
                if (IsInventoryActionBlockedByCarry())
                {
                    return;
                }

                inventory.UseHeld(gameObject);
            }

            if (WasPressed(input != null && input.drop, ref previousDrop) && inventory != null && inventory.HasItem)
            {
                if (IsInventoryActionBlockedByCarry())
                {
                    return;
                }

                inventory.Drop(gameObject);
            }

            if (inventory != null && inventory.ItemCount > 0 && input != null && input.slot >= 0)
            {
                if (IsInventoryActionBlockedByCarry())
                {
                    input.slot = -1;
                    return;
                }

                int maxSelectable = Mathf.Min(6, inventory.MaxSlots);
                int slotIndex = Mathf.Clamp(input.slot, 0, maxSelectable - 1);
                inventory.TrySelectSlot(slotIndex);
                input.slot = -1;
            }
        }

        private void UpdateFocus(Transform ignoredRoot)
        {
            Ray ray = viewCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

            RaycastHit[] hits = Physics.RaycastAll(ray, interactDistance, interactMask, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];
                    if (ignoredRoot != null && hit.collider != null && hit.collider.transform.IsChildOf(ignoredRoot))
                    {
                        continue;
                    }

                    IInteractable interactable = FindInteractable(hit.collider);
                    if (interactable != null)
                    {
                        SetFocus(hit.collider.gameObject, interactable);
                        if (drawDebugRay)
                        {
                            Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.green);
                        }
                        return;
                    }
                }
            }

            ClearFocus();

            if (drawDebugRay)
            {
                Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red);
            }
        }

        private static IInteractable FindInteractable(Collider collider)
        {
            if (collider.TryGetComponent(out IInteractable direct))
            {
                return direct;
            }

            if (collider.attachedRigidbody != null &&
                collider.attachedRigidbody.TryGetComponent(out IInteractable rigidbodyOwner))
            {
                return rigidbodyOwner;
            }

            Transform parent = collider.transform.parent;
            while (parent != null)
            {
                if (parent.TryGetComponent(out IInteractable parentInteractable))
                {
                    return parentInteractable;
                }
                parent = parent.parent;
            }

            return null;
        }

        private bool TryCompleteCarriedRescueAtCurrentTarget()
        {
            if (!(currentInteractable is ISafeZoneTarget safeZone))
            {
                return false;
            }

            IRescuableTarget carriedTarget = cachedCarriedRescuable;
            if (carriedTarget == null)
            {
                return false;
            }

            Vector3 fallbackDropPosition = transform.position + transform.right * 0.75f;
            Vector3 dropPosition;

            if (safeZone.TryClaimSlot(gameObject, out Vector3 slotPosition))
            {
                dropPosition = slotPosition;
            }
            else
            {
                dropPosition = safeZone.GetDropPoint(fallbackDropPosition);
            }

            carriedTarget.CompleteRescueAt(dropPosition, safeZone.GetSlotRotation(dropPosition));
            safeZone.OccupySlotAt(dropPosition);
            safeZone.ReleaseSlot(gameObject);
            return true;
        }

        private IRescuableTarget FindPlayerCarriedRescuable()
        {
            foreach (IRescuableTarget rescuable in BotRuntimeRegistry.ActiveRescuableTargets)
            {
                if (rescuable == null)
                {
                    continue;
                }

                if (!rescuable.IsCarried || rescuable.ActiveRescuer != gameObject)
                {
                    continue;
                }

                return rescuable;
            }

            return null;
        }

        private static IGrabbable FindGrabbable(Collider collider)
        {
            if (collider.TryGetComponent(out IGrabbable direct))
            {
                return direct;
            }

            if (collider.attachedRigidbody != null &&
                collider.attachedRigidbody.TryGetComponent(out IGrabbable rigidbodyOwner))
            {
                return rigidbodyOwner;
            }

            Transform parent = collider.transform.parent;
            while (parent != null)
            {
                if (parent.TryGetComponent(out IGrabbable parentGrabbable))
                {
                    return parentGrabbable;
                }
                parent = parent.parent;
            }

            return null;
        }

        private void SetFocus(GameObject target, IInteractable interactable)
        {
            if (currentTarget == target && currentInteractable == interactable)
            {
                return;
            }

            currentTarget = target;
            currentInteractable = interactable;
            UpdateOutlineHighlight(target, interactable);
        }

        private void ClearFocus()
        {
            if (currentTarget == null && currentInteractable == null)
            {
                return;
            }

            currentTarget = null;
            currentInteractable = null;
            ClearOutlineHighlight();
        }

        private void TryGrab()
        {
            if (grabbedBody != null || viewCamera == null)
            {
                return;
            }

            if (inventory != null && inventory.HasItem)
            {
                return;
            }

            ResolveGrabPoint();
            if (grabPoint == null)
            {
                return;
            }

            Ray ray = viewCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, grabDistance, grabMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            IGrabbable grabbable = FindGrabbable(hit.collider);
            if (grabbable == null)
            {
                return;
            }

            Rigidbody targetBody = hit.rigidbody;
            if (targetBody == null)
            {
                return;
            }

            if (targetBody.mass > maxGrabMass)
            {
                return;
            }

            grabbedBody = targetBody;
            grabbedOriginalParent = grabbedBody.transform.parent;
            grabbedOriginalIsKinematic = grabbedBody.isKinematic;
            grabbedOriginalDetectCollisions = grabbedBody.detectCollisions;

            ZeroRigidbodyVelocityIfDynamic(grabbedBody);
            grabbedBody.isKinematic = true;
            grabbedBody.detectCollisions = false;

            Transform grabbedTransform = grabbedBody.transform;
            grabbedTransform.SetParent(grabPoint, false);
            grabbedTransform.localPosition = Vector3.zero;
            grabbedTransform.localRotation = Quaternion.identity;
            grabbedPlacementOverride = FindCustomGrabPlacement(grabbedTransform);
            CacheGrabbedBurnSources(grabbedTransform);
            grabbedPlacementOverride?.OnGrabStarted();
            RebuildGrabPreview();
        }

        private void ToggleGrab()
        {
            if (IsGrabActive)
            {
                ReleaseGrab();
                return;
            }

            TryGrab();
        }

        private void ReleaseGrab()
        {
            if (grabbedBody != null)
            {
                Transform grabbedTransform = grabbedBody.transform;
                grabbedTransform.SetParent(grabbedOriginalParent, true);

                grabbedBody.isKinematic = grabbedOriginalIsKinematic;
                grabbedBody.detectCollisions = grabbedOriginalDetectCollisions;
                ZeroRigidbodyVelocityIfDynamic(grabbedBody);
            }

            grabbedPlacementOverride?.OnGrabCancelled();

            DestroyGrabPreview();
            grabbedBurnableSources.Clear();
            grabbedBody = null;
            grabbedOriginalParent = null;
            grabbedPlacementOverride = null;
        }

        private void ResolveGrabPoint()
        {
            if (grabPoint != null)
            {
                return;
            }

            grabPoint = FindChildTransformByName("GrabPoint");
            if (grabPoint != null || viewCamera == null)
            {
                ApplyGrabPointPosition();
                return;
            }

            GameObject runtimeGrabPoint = new GameObject("RuntimeGrabPoint");
            grabPoint = runtimeGrabPoint.transform;
            grabPoint.SetParent(viewCamera.transform, false);
            ApplyGrabPointPosition();
            grabPoint.localRotation = Quaternion.identity;
        }

        private Transform FindChildTransformByName(string childName)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == childName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void ApplyGrabPointPosition()
        {
            if (grabPoint == null)
            {
                return;
            }

            grabPoint.localPosition = grabPointLocalPosition;
        }

        private void BlockGameplayActionsWhileGrabbed()
        {
            if (input != null)
            {
                input.ClearGameplayActionInputs();
            }
        }

        private void UpdateGrabPlacementPreview()
        {
            if (!enableGrabPlacement || !TryResolveGrabPlacementPose(out Vector3 position, out Quaternion rotation))
            {
                SetGrabPreviewVisible(false);
                return;
            }

            GameObject previewRoot = EnsureGrabPreview();
            if (previewRoot == null)
            {
                return;
            }

            previewRoot.transform.SetPositionAndRotation(position, rotation);
            SetGrabPreviewVisible(true);
        }

        private void TryPlaceGrabbed()
        {
            if (!enableGrabPlacement || !TryResolveGrabPlacementPose(out Vector3 position, out Quaternion rotation))
            {
                return;
            }

            ReleaseGrabAt(position, rotation);
        }

        private bool TryResolveGrabPlacementPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;

            if (grabbedBody == null || viewCamera == null)
            {
                return false;
            }

            if (grabbedPlacementOverride != null)
            {
                return grabbedPlacementOverride.TryGetGrabPlacementPose(
                    viewCamera.transform,
                    grabPlacementMask,
                    grabPlacementDistance,
                    out position,
                    out rotation);
            }

            float maxDistance = Mathf.Max(0.1f, grabPlacementDistance > 0f ? grabPlacementDistance : grabDistance);
            Ray ray = viewCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, grabPlacementMask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(grabbedBody.transform))
                {
                    continue;
                }

                rotation = GetGrabPlacementRotation();
                float surfaceOffset = GetGrabbedPlacementSurfaceOffset(hit.normal);
                position = hit.point + hit.normal * surfaceOffset;
                return true;
            }

            return false;
        }

        private float GetGrabbedPlacementSurfaceOffset(Vector3 surfaceNormal)
        {
            if (grabbedBody == null)
            {
                return grabPlacementSurfacePadding;
            }

            Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f
                ? surfaceNormal.normalized
                : Vector3.up;
            float maxExtent = 0f;
            Collider[] colliders = grabbedBody.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                float extent =
                    Mathf.Abs(normal.x) * bounds.extents.x +
                    Mathf.Abs(normal.y) * bounds.extents.y +
                    Mathf.Abs(normal.z) * bounds.extents.z;
                if (extent > maxExtent)
                {
                    maxExtent = extent;
                }
            }

            return maxExtent + Mathf.Max(0f, grabPlacementSurfacePadding);
        }

        private Quaternion GetGrabPlacementRotation()
        {
            Vector3 forward = viewCamera != null ? viewCamera.transform.forward : transform.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void ReleaseGrabAt(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (grabbedBody == null)
            {
                return;
            }

            Transform grabbedTransform = grabbedBody.transform;
            grabbedTransform.SetParent(grabbedOriginalParent, true);
            grabbedTransform.SetPositionAndRotation(worldPosition, worldRotation);

            grabbedBody.detectCollisions = grabbedOriginalDetectCollisions;

            if (grabbedPlacementOverride != null)
            {
                grabbedPlacementOverride.OnGrabPlaced(worldPosition, worldRotation);
            }
            else
            {
                grabbedBody.isKinematic = grabbedOriginalIsKinematic;
                ZeroRigidbodyVelocityIfDynamic(grabbedBody);
            }

            DestroyGrabPreview();
            grabbedBurnableSources.Clear();
            grabbedBody = null;
            grabbedOriginalParent = null;
            grabbedPlacementOverride = null;
        }

        private static void ZeroRigidbodyVelocityIfDynamic(Rigidbody body)
        {
            if (body == null || body.isKinematic)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private GameObject EnsureGrabPreview()
        {
            if (grabbedPreviewRoot != null)
            {
                return grabbedPreviewRoot;
            }

            if (grabbedBody == null)
            {
                return null;
            }

            grabbedPreviewRoot = Instantiate(grabbedBody.gameObject);
            grabbedPreviewRoot.name = "GrabPlacementPreview";
            grabbedPreviewRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            grabbedPreviewRoot.SetActive(false);
            PrepareGrabPreviewClone(grabbedPreviewRoot);
            SetGrabPreviewVisible(false);
            return grabbedPreviewRoot;
        }

        private void RebuildGrabPreview()
        {
            DestroyGrabPreview();
            EnsureGrabPreview();
        }

        private void DestroyGrabPreview()
        {
            if (grabbedPreviewRoot != null)
            {
                DestroyRuntimeSafe(grabbedPreviewRoot);
                grabbedPreviewRoot = null;
            }
        }

        private void SetGrabPreviewVisible(bool visible)
        {
            if (grabbedPreviewRoot != null && grabbedPreviewRoot.activeSelf != visible)
            {
                grabbedPreviewRoot.SetActive(visible);
            }
        }

        private void PrepareGrabPreviewClone(GameObject previewObject)
        {
            if (previewObject == null)
            {
                return;
            }

            previewObject.tag = "Untagged";
            previewObject.transform.SetParent(null, true);
            Component[] components = previewObject.GetComponentsInChildren<Component>(true);

            // Remove behaviours before physics components so RequireComponent dependencies
            // on preview clones do not spam errors while being stripped down.
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null ||
                    component is Transform ||
                    component is Renderer ||
                    component is MeshFilter)
                {
                    continue;
                }

                if (component is MonoBehaviour)
                {
                    DestroyRuntimeSafe(component);
                }
            }

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component is Transform)
                {
                    continue;
                }

                if (component is Renderer renderer)
                {
                    Material previewMaterial = GetGrabPreviewMaterial();
                    Material[] previewMaterials = new Material[renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 0];
                    for (int materialIndex = 0; materialIndex < previewMaterials.Length; materialIndex++)
                    {
                        previewMaterials[materialIndex] = previewMaterial;
                    }

                    if (previewMaterials.Length > 0)
                    {
                        renderer.sharedMaterials = previewMaterials;
                    }

                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    renderer.allowOcclusionWhenDynamic = false;
                    continue;
                }

                if (component is MeshFilter)
                {
                    continue;
                }

                if (component is MonoBehaviour)
                {
                    continue;
                }

                DestroyRuntimeSafe(component);
            }
        }

        private Material GetGrabPreviewMaterial()
        {
            if (grabbedPreviewMaterial != null)
            {
                ApplyPreviewMaterialColor(grabbedPreviewMaterial);
                return grabbedPreviewMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            grabbedPreviewMaterial = new Material(shader)
            {
                name = "GrabPlacementPreviewMaterial"
            };

            ApplyPreviewMaterialColor(grabbedPreviewMaterial);
            if (grabbedPreviewMaterial.HasProperty("_Surface"))
            {
                grabbedPreviewMaterial.SetFloat("_Surface", 1f);
            }

            if (grabbedPreviewMaterial.HasProperty("_Blend"))
            {
                grabbedPreviewMaterial.SetFloat("_Blend", 0f);
            }

            grabbedPreviewMaterial.SetOverrideTag("RenderType", "Transparent");
            grabbedPreviewMaterial.renderQueue = (int)RenderQueue.Transparent;
            grabbedPreviewMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return grabbedPreviewMaterial;
        }

        private void ApplyPreviewMaterialColor(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.color = grabPlacementPreviewColor;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", grabPlacementPreviewColor);
            }
        }

        private static void DestroyRuntimeSafe(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private bool IsCarryRestricted()
        {
            return playerActionLock != null && playerActionLock.HasCarryRestriction;
        }

        private bool IsInventoryActionBlockedByCarry()
        {
            return playerActionLock != null && !playerActionLock.AllowsInventoryActions;
        }

        private bool IsGeneralInteractionBlockedByCarry(IInteractable interactable)
        {
            if (playerActionLock == null || playerActionLock.AllowsGeneralInteraction)
            {
                return false;
            }

            if (playerActionLock.AllowsSafeZoneInteractionOnly &&
                (interactable is ISafeZoneTarget || CanInteractWhileHandsOccupied(interactable)))
            {
                return false;
            }

            return true;
        }

        private static bool CanInteractWhileGrabbed(IInteractable interactable)
        {
            return CanInteractWhileHandsOccupied(interactable);
        }

        private static bool CanInteractWhileHandsOccupied(IInteractable interactable)
        {
            return interactable is Door;
        }

        private bool TryStartWindowClimbAtCurrentTarget()
        {
            if (currentTarget == null)
            {
                return false;
            }

            Window targetWindow = FindWindow(currentTarget.transform);
            if (targetWindow == null)
            {
                return false;
            }

            if (AreHandsOccupied)
            {
                return false;
            }

            return targetWindow.TryStartClimbOver(gameObject);
        }

        private static Window FindWindow(Transform origin)
        {
            Transform current = origin;
            while (current != null)
            {
                if (current.TryGetComponent(out Window window))
                {
                    return window;
                }

                current = current.parent;
            }

            return null;
        }

        private static bool WasPressed(bool current, ref bool previous)
        {
            bool pressed = current && !previous;
            previous = current;
            return pressed;
        }

        private void OnDisable()
        {
            ReleaseGrab();
            DestroyRuntimeSafe(grabbedPreviewMaterial);
            grabbedPreviewMaterial = null;
            ClearOutlineHighlight();
        }

        private void UpdateOutlineHighlight(GameObject target, IInteractable interactable)
        {
            if (!enableOutlineHighlight)
            {
                ClearOutlineHighlight();
                return;
            }

            GameObject highlightRoot = ResolveHighlightRoot(target, interactable);
            if (highlightRoot == outlinedTargetRoot)
            {
                return;
            }

            ClearOutlineHighlight();
            if (highlightRoot == null)
            {
                return;
            }

            outlinedTargetRoot = highlightRoot;
            outlinedRenderers = highlightRoot.GetComponentsInChildren<Renderer>(true);
            outlinedRendererHadBit = new bool[outlinedRenderers.Length];

            uint outlineBit = 1u << outlineRenderingLayer;
            for (int i = 0; i < outlinedRenderers.Length; i++)
            {
                Renderer renderer = outlinedRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                outlinedRendererHadBit[i] = (renderer.renderingLayerMask & outlineBit) != 0;
                renderer.renderingLayerMask |= outlineBit;
            }
        }

        private void ClearOutlineHighlight()
        {
            if (outlinedRenderers != null && outlinedRendererHadBit != null)
            {
                uint outlineBit = 1u << outlineRenderingLayer;
                int count = Mathf.Min(outlinedRenderers.Length, outlinedRendererHadBit.Length);
                for (int i = 0; i < count; i++)
                {
                    Renderer renderer = outlinedRenderers[i];
                    if (renderer == null || outlinedRendererHadBit[i])
                    {
                        continue;
                    }

                    renderer.renderingLayerMask &= ~outlineBit;
                }
            }

            outlinedTargetRoot = null;
            outlinedRenderers = null;
            outlinedRendererHadBit = null;
        }

        private static GameObject ResolveHighlightRoot(GameObject target, IInteractable interactable)
        {
            if (interactable is Component interactableComponent)
            {
                return interactableComponent.gameObject;
            }

            return target;
        }

        private static void NotifyInteractionSignalRelay(GameObject target, IInteractable interactable, GameObject interactor)
        {
            GameObject relayRoot = ResolveHighlightRoot(target, interactable);
            if (relayRoot == null)
            {
                return;
            }

            MissionInteractionSignalRelay relay = relayRoot.GetComponent<MissionInteractionSignalRelay>();
            if (relay == null)
            {
                relay = relayRoot.GetComponentInParent<MissionInteractionSignalRelay>();
            }

            relay?.NotifyInteracted(interactor);
        }

        private void ApplyHeldBurnDamage()
        {
            if (playerVitals == null || !playerVitals.IsAlive || grabbedBody == null)
            {
                return;
            }

            if (grabbedBurnableSources.Count == 0)
            {
                CacheGrabbedBurnSources(grabbedBody.transform);
                if (grabbedBurnableSources.Count == 0)
                {
                    return;
                }
            }

            float maxDamagePerSecond = 0f;
            for (int i = grabbedBurnableSources.Count - 1; i >= 0; i--)
            {
                MonoBehaviour source = grabbedBurnableSources[i];
                if (source == null || source is not IBurnable burnable)
                {
                    grabbedBurnableSources.RemoveAt(i);
                    continue;
                }

                maxDamagePerSecond = Mathf.Max(maxDamagePerSecond, burnable.CurrentFireContactDamagePerSecond);
            }

            if (maxDamagePerSecond > 0f)
            {
                playerVitals.TakeDamage(maxDamagePerSecond * Time.deltaTime);
            }
        }

        private void CacheGrabbedBurnSources(Transform grabbedTransform)
        {
            grabbedBurnableSources.Clear();
            if (grabbedTransform == null)
            {
                return;
            }

            if (grabbedTransform.TryGetComponent(out MonoBehaviour directBurnableBehaviour) &&
                directBurnableBehaviour is IBurnable directBurnable &&
                directBurnable.CurrentFireContactDamagePerSecond > 0f)
            {
                grabbedBurnableSources.Add(directBurnableBehaviour);
                return;
            }

            MonoBehaviour[] behaviours = grabbedTransform.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour is IBurnable burnable && burnable.CurrentFireContactDamagePerSecond > 0f)
                {
                    grabbedBurnableSources.Add(behaviour);
                }
            }

            if (!hasLoggedLegacyGrabFireWarning && grabbedBurnableSources.Count > 0)
            {
                hasLoggedLegacyGrabFireWarning = true;
                Debug.LogWarning(
                    $"{nameof(FPSInteractionSystem)} detected burnable sources on grabbed objects through legacy-compatible paths. Prefer node-based fire integrations for new interactable content.",
                    this);
            }
        }

        private static ICustomGrabPlacement FindCustomGrabPlacement(Transform grabbedTransform)
        {
            if (grabbedTransform == null)
            {
                return null;
            }

            if (grabbedTransform.TryGetComponent(out ICustomGrabPlacement direct))
            {
                return direct;
            }

            Transform parent = grabbedTransform.parent;
            while (parent != null)
            {
                if (parent.TryGetComponent(out ICustomGrabPlacement parentPlacement))
                {
                    return parentPlacement;
                }

                parent = parent.parent;
            }

            return null;
        }

        private static float ResolveGrabbedBodyWeightKg(Rigidbody body)
        {
            if (body == null)
            {
                return 0f;
            }

            return ResolveMovementWeightKg(body.transform, body);
        }

        private static float ResolveRescuableCarryWeightKg(IRescuableTarget rescuable)
        {
            if (!(rescuable is Component component))
            {
                return 0f;
            }

            Rigidbody fallbackBody = component.GetComponent<Rigidbody>();
            return ResolveMovementWeightKg(component.transform, fallbackBody);
        }

        private static float ResolveMovementWeightKg(Transform origin, Rigidbody fallbackBody)
        {
            IMovementWeightSource weightSource = FindMovementWeightSource(origin);
            if (weightSource != null)
            {
                return Mathf.Max(0f, weightSource.MovementWeightKg);
            }

            if (fallbackBody == null && origin != null)
            {
                fallbackBody = origin.GetComponent<Rigidbody>();
            }

            return fallbackBody != null
                ? Mathf.Max(0f, fallbackBody.mass)
                : 0f;
        }

        private static IMovementWeightSource FindMovementWeightSource(Transform origin)
        {
            Transform current = origin;
            while (current != null)
            {
                IMovementWeightSource weightSource = current.GetComponent<IMovementWeightSource>();
                if (weightSource != null)
                {
                    return weightSource;
                }

                current = current.parent;
            }

            return null;
        }
    }
}
