using System;
using System.Collections.Generic;
using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StarterAssets
{
    public class FPSCommandSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private FPSInteractionSystem interactionSystem;

        [Header("Selection")]
        [SerializeField] private float selectionDistance = 12f;
        [SerializeField] private LayerMask commandableMask = ~0;
        [SerializeField, Range(0, 31)] private int selectedRenderingLayer = 7;

        [Header("Move Command")]
        [SerializeField] private KeyCode moveCommandKey = KeyCode.H;
        [SerializeField] private KeyCode cancelCommandKey = KeyCode.Escape;
        [SerializeField] private KeyCode cancelAllFollowKey = KeyCode.X;
        [SerializeField] private KeyCode toggleBotOutlineKey = KeyCode.Z;
        [SerializeField] private float destinationRayDistance = 200f;
        [SerializeField] private LayerMask destinationMask = ~0;

        [Header("Bot Outline")]
        [SerializeField, Range(0, 31)] private int botOutlineRenderingLayer = 7;

        [Header("Debug")]
        [SerializeField] private bool drawDebugRay;
        [SerializeField] private bool showDebugOverlay = true;
        [SerializeField] private bool logCommandSelection = true;
        [SerializeField] private Vector2 debugOverlayOffset = new Vector2(16f, 16f);

        [Header("Extinguish Command")]
        [SerializeField] private float extinguishScanRadius = 2.25f;
        [SerializeField] private LayerMask extinguishScanMask = ~0;

        [Header("Extinguish Debug")]
        [SerializeField] private bool spawnExtinguishScanDebugSphere = true;
        [SerializeField] private float extinguishDebugSphereLifetime = 2.5f;
        [SerializeField] private Color extinguishDebugSphereColor = new Color(1f, 0.45f, 0.1f, 0.2f);

        [Header("Rescue Debug")]
        [SerializeField] private bool spawnRescueDebugSphere = true;
        [SerializeField] private float fallbackRescueDebugSphereRadius = 2.25f;
        [SerializeField] private Color rescueDebugSphereColor = new Color(0.2f, 0.85f, 1f, 0.2f);

        [Header("Selection Wheel")]
        [SerializeField] private WheelSelector wheelSelector;

        private readonly BotCommandState commandState = new BotCommandState();
        private readonly ClickReleaseGate destinationConfirmClickGate = new ClickReleaseGate();
        private GUIStyle debugGuiStyle;

        private ICommandable hoveredCommandable;
        private GameObject hoveredCommandTarget;
        [SerializeField] private GameObject selectedCommandTarget;
        private GameObject currentOutlineRoot;
        private Renderer[] currentOutlineRenderers;
        private bool[] currentOutlineRendererHadBit;
        private Vector3 lastPreviewPoint;
        private bool hasPreviewPoint;

        private bool isAwaitingCommandSelection;
        private ICommandable pendingCommandable;
        private GameObject pendingCommandTarget;

        public GameObject HoveredCommandTarget => hoveredCommandTarget;
        public GameObject SelectedCommandTarget => selectedCommandTarget;
        public bool IsAwaitingDestination => commandState.IsAwaitingTarget;

        private void Awake()
        {
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            if (interactionSystem == null)
            {
                interactionSystem = GetComponent<FPSInteractionSystem>();
            }

            if (wheelSelector != null)
            {
                wheelSelector.OnOptionSelected += OnWheelOptionSelected;
            }

            BotOutlineVisibilityManager.ConfigureRenderingLayer(botOutlineRenderingLayer);
        }

        private void OnDestroy()
        {
            if (wheelSelector != null)
            {
                wheelSelector.OnOptionSelected -= OnWheelOptionSelected;
            }
        }

        private void Update()
        {
            if (viewCamera == null)
            {
                return;
            }

            if (Input.GetKeyDown(toggleBotOutlineKey))
            {
                ToggleBotOutlineVisibility();
            }

            if (Input.GetKeyDown(cancelAllFollowKey))
            {
                CancelAllFollowCommands();
            }

            if (interactionSystem != null && interactionSystem.IsGrabActive)
            {
                if (commandState.IsAwaitingTarget)
                {
                    CancelPendingCommand();
                }

                hoveredCommandable = null;
                hoveredCommandTarget = null;
                return;
            }

            UpdateHoveredCommandable();

            if (Input.GetKeyDown(cancelCommandKey))
            {
                CancelPendingCommand();
                return;
            }

            if (isAwaitingCommandSelection)
            {
                // waiting for the wheel command choice
                return;
            }

            if (!commandState.IsAwaitingTarget)
            {
                if (Input.GetKeyDown(moveCommandKey))
                {
                    TryStartCommandSelection();
                }

                return;
            }

            UpdatePreviewPoint();

            if (destinationConfirmClickGate.ShouldProcessClick(Input.GetMouseButtonDown(0), Input.GetMouseButton(0)))
            {
                TryConfirmPendingCommand();
            }
        }

        private void UpdateHoveredCommandable()
        {
            hoveredCommandable = null;
            hoveredCommandTarget = null;

            if (interactionSystem != null &&
                interactionSystem.CurrentTarget != null &&
                FindCommandable(interactionSystem.CurrentTarget.transform, out hoveredCommandTarget) is ICommandable focusedTarget)
            {
                hoveredCommandable = focusedTarget;
                return;
            }

            Ray ray = GetCenterRay();
            if (!Physics.Raycast(ray, out RaycastHit hit, selectionDistance, commandableMask, QueryTriggerInteraction.Ignore))
            {
                DrawDebugRay(ray, selectionDistance, Color.red);
                return;
            }

            hoveredCommandable = FindCommandable(hit.collider, out hoveredCommandTarget);
            if (hoveredCommandable == null)
            {
                DrawDebugRay(ray, selectionDistance, Color.yellow);
                return;
            }

            DrawDebugRay(ray, hit.distance, Color.green);
        }

        private void TryStartCommandSelection()
        {
            if (hoveredCommandable == null)
            {
                return;
            }

            pendingCommandable = hoveredCommandable;
            pendingCommandTarget = hoveredCommandTarget;
            isAwaitingCommandSelection = true;
            UpdateTargetOutline(pendingCommandTarget);

            if (wheelSelector != null)
            {
                wheelSelector.OpenWheel();
            }

            if (logCommandSelection)
            {
                Debug.Log($"[FPSCommandSystem] Selected bot '{GetTargetName(pendingCommandTarget)}' for command selection.", this);
            }
        }

        private void OnWheelOptionSelected(int selectedIndex)
        {
            if (!isAwaitingCommandSelection || pendingCommandable == null || pendingCommandTarget == null)
            {
                return;
            }

            BotCommandType commandType = MapWheelIndexToCommand(selectedIndex);
            if (commandType == BotCommandType.None)
            {
                if (logCommandSelection)
                {
                    Debug.LogWarning($"[FPSCommandSystem] Unknown command wheel selection index {selectedIndex}.");
                }
                ResetCommandSelection();
                return;
            }

            if (!commandState.TryBegin(pendingCommandable, commandType))
            {
                if (logCommandSelection)
                {
                    Debug.LogWarning($"[FPSCommandSystem] Command '{commandType}' is not supported by '{GetTargetName(pendingCommandTarget)}'.");
                }
                ResetCommandSelection();
                return;
            }

            selectedCommandTarget = pendingCommandTarget;
            UpdateTargetOutline(selectedCommandTarget);
            isAwaitingCommandSelection = false;
            pendingCommandable = null;
            pendingCommandTarget = null;

            if (commandType == BotCommandType.Follow)
            {
                destinationConfirmClickGate.Reset();
                TryConfirmImmediateCommand(commandType);
                return;
            }

            destinationConfirmClickGate.BlockUntilRelease();
            UpdatePreviewPoint();

            if (logCommandSelection)
            {
                Debug.Log($"[FPSCommandSystem] Selected bot '{GetTargetName(selectedCommandTarget)}' with command '{commandType}'.", this);
            }
        }

        private BotCommandType MapWheelIndexToCommand(int selectedIndex)
        {
            switch (selectedIndex)
            {
                case 0:
                    return BotCommandType.Move;
                case 1:
                    return BotCommandType.Extinguish;
                case 2:
                    return BotCommandType.Follow;
                case 3:
                    return BotCommandType.Rescue;
                default:
                    return BotCommandType.None;
            }
        }

        private void TryConfirmImmediateCommand(BotCommandType commandType)
        {
            if (commandState.TryConfirm(transform.position))
            {
                if (logCommandSelection)
                {
                    Debug.Log($"[FPSCommandSystem] Issued '{commandType}' to '{GetTargetName(selectedCommandTarget)}'.", this);
                }

                selectedCommandTarget = null;
                UpdateTargetOutline(null);
                hasPreviewPoint = false;
            }
            else if (logCommandSelection)
            {
                Debug.LogWarning($"[FPSCommandSystem] Immediate command '{commandType}' failed for '{GetTargetName(selectedCommandTarget)}'.", this);
            }
        }

        private void ResetCommandSelection()
        {
            isAwaitingCommandSelection = false;
            pendingCommandable = null;
            pendingCommandTarget = null;
            destinationConfirmClickGate.Reset();
            if (wheelSelector != null)
            {
                wheelSelector.CloseWheel();
            }
            selectedCommandTarget = null;
            UpdateTargetOutline(null);
            hasPreviewPoint = false;
        }

        private void TryConfirmPendingCommand()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (!TryResolvePendingCommand(out BotCommandType commandType, out Vector3 destination, out IPickupable movePickupTarget, out RaycastHit primaryHit))
            {
                return;
            }

            global::BotCommandAgent botCommandAgent =
                commandState.SelectedCommandable as global::BotCommandAgent ??
                (selectedCommandTarget != null ? selectedCommandTarget.GetComponent<global::BotCommandAgent>() : null);

            if (commandType == BotCommandType.Move && movePickupTarget != null && botCommandAgent != null)
            {
                if (botCommandAgent.TryIssueMoveToPickup(movePickupTarget))
                {
                    commandState.Cancel();
                    if (logCommandSelection)
                    {
                        Debug.Log($"[FPSCommandSystem] Issued 'Move' to '{GetTargetName(selectedCommandTarget)}' for pickup target.", this);
                    }

                    selectedCommandTarget = null;
                    UpdateTargetOutline(null);
                    return;
                }

                if (logCommandSelection)
                {
                    Debug.LogWarning($"[FPSCommandSystem] Move pickup command failed for '{GetTargetName(selectedCommandTarget)}'.", this);
                }

                return;
            }

            if (botCommandAgent != null)
            {
                botCommandAgent.SetMovePickupTarget(commandType == BotCommandType.Move ? movePickupTarget : null);
            }

            if (commandType == BotCommandType.Extinguish && botCommandAgent != null)
            {
                SpawnExtinguishScanDebugSphere(primaryHit.point);

                if (!TryResolveExtinguishTargetFromArea(
                    primaryHit.point,
                    out Vector3 extinguishDestination,
                    out BotExtinguishCommandMode extinguishMode,
                    out IFireTarget pointFireTarget,
                    out IFireGroupTarget fireGroupTarget))
                {
                    if (logCommandSelection)
                    {
                        Debug.LogWarning($"[FPSCommandSystem] Extinguish found no burning fire or active fire group near {primaryHit.point}.", this);
                    }

                    return;
                }

                if (botCommandAgent.TryIssueExtinguishCommand(
                    extinguishDestination,
                    extinguishMode,
                    pointFireTarget,
                    fireGroupTarget))
                {
                    commandState.Cancel();
                    if (logCommandSelection)
                    {
                        Debug.Log($"[FPSCommandSystem] Issued '{commandType}' to '{GetTargetName(selectedCommandTarget)}' at {extinguishDestination} with mode '{extinguishMode}'.", this);
                    }

                    selectedCommandTarget = null;
                    UpdateTargetOutline(null);
                    return;
                }

                if (logCommandSelection)
                {
                    Debug.LogWarning($"[FPSCommandSystem] Command confirmation failed for '{GetTargetName(selectedCommandTarget)}'.", this);
                }

                return;
            }

            if (commandType == BotCommandType.Rescue)
            {
                SpawnRescueDebugSphere(primaryHit.point, botCommandAgent);
            }

            if (commandState.TryConfirm(commandType, destination))
            {
                if (logCommandSelection)
                {
                    Debug.Log($"[FPSCommandSystem] Issued '{commandType}' to '{GetTargetName(selectedCommandTarget)}' at {destination}.", this);
                }

                selectedCommandTarget = null;
                UpdateTargetOutline(null);
            }
            else if (logCommandSelection)
            {
                Debug.LogWarning($"[FPSCommandSystem] Command confirmation failed for '{GetTargetName(selectedCommandTarget)}'.", this);
            }
        }

        private void CancelPendingCommand()
        {
            if (isAwaitingCommandSelection)
            {
                if (logCommandSelection && pendingCommandTarget != null)
                {
                    Debug.Log($"[FPSCommandSystem] Cancelled pending command selection for '{GetTargetName(pendingCommandTarget)}'.", this);
                }

                ResetCommandSelection();
                return;
            }

            if (logCommandSelection && selectedCommandTarget != null)
            {
                Debug.Log($"[FPSCommandSystem] Cancelled command for '{GetTargetName(selectedCommandTarget)}'.", this);
            }

            commandState.Cancel();
            destinationConfirmClickGate.Reset();
            selectedCommandTarget = null;
            UpdateTargetOutline(null);
            hasPreviewPoint = false;
            if (wheelSelector != null)
            {
                wheelSelector.CloseWheel();
            }
        }

        private void ToggleBotOutlineVisibility()
        {
            BotOutlineVisibilityManager.ConfigureRenderingLayer(botOutlineRenderingLayer);
            bool nextVisible = !BotOutlineVisibilityManager.OutlinesVisible;
            BotOutlineVisibilityManager.SetOutlinesVisible(nextVisible);

            if (logCommandSelection)
            {
                string state = nextVisible ? "visible" : "hidden";
                Debug.Log($"[FPSCommandSystem] Bot outline is now {state}.", this);
            }
        }

        private void CancelAllFollowCommands()
        {
            int canceledCount = 0;
            foreach (BotCommandAgent bot in BotRuntimeRegistry.ActiveCommandAgents)
            {
                if (bot != null && bot.TryCancelFollowCommand())
                {
                    canceledCount++;
                }
            }

            if (logCommandSelection)
            {
                Debug.Log($"[FPSCommandSystem] Cancelled Follow on {canceledCount} bot(s).", this);
            }
        }

        private void UpdatePreviewPoint()
        {
            hasPreviewPoint = TryGetDestinationPoint(out lastPreviewPoint);
        }

        private bool TryResolvePendingCommand(out BotCommandType commandType, out Vector3 destination, out IPickupable movePickupTarget, out RaycastHit primaryHit)
        {
            commandType = commandState.PendingCommand;
            movePickupTarget = null;
            if (!TryGetDestinationHit(out primaryHit))
            {
                destination = default;
                primaryHit = default;
                return false;
            }

            destination = primaryHit.point;
            if (commandType != BotCommandType.Move)
            {
                return true;
            }

            if (logCommandSelection)
            {
                Debug.Log($"[FPSCommandSystem] Move ray primary hit '{GetHitDebugName(primaryHit)}' at {primaryHit.point}.", this);
            }

            if (TryGetContextualHits(out RaycastHit[] contextualHits) &&
                TryResolveContextualMoveCommand(contextualHits, out BotCommandType contextualCommand, out Vector3 contextualDestination, out movePickupTarget))
            {
                commandType = contextualCommand;
                destination = contextualDestination;

                if (logCommandSelection)
                {
                    string contextualName = movePickupTarget is Component pickupComponent && pickupComponent != null
                        ? pickupComponent.name
                        : destination.ToString();
                    Debug.Log($"[FPSCommandSystem] Move contextual resolve -> '{commandType}' via '{contextualName}'.", this);
                }
            }
            else if (logCommandSelection)
            {
                Debug.Log("[FPSCommandSystem] Move contextual resolve -> plain Move.", this);
            }

            return true;
        }

        private bool TryGetDestinationPoint(out Vector3 destination)
        {
            if (TryGetDestinationHit(out RaycastHit hit))
            {
                destination = hit.point;
                return true;
            }

            destination = default;
            return false;
        }

        private bool TryResolveExtinguishTargetFromArea(
            Vector3 worldPoint,
            out Vector3 destination,
            out BotExtinguishCommandMode mode,
            out IFireTarget pointFireTarget,
            out IFireGroupTarget fireGroupTarget)
        {
            destination = worldPoint;
            mode = BotExtinguishCommandMode.Auto;
            pointFireTarget = null;
            fireGroupTarget = null;

            HashSet<IFireTarget> pointFires = new HashSet<IFireTarget>();
            HashSet<IFireGroupTarget> fireGroups = new HashSet<IFireGroupTarget>();
            IFireTarget nearestFire = null;
            IFireGroupTarget nearestGroup = null;
            float nearestFireDistanceSq = float.PositiveInfinity;
            float nearestGroupDistanceSq = float.PositiveInfinity;
            float scanRadiusSq = Mathf.Max(0.1f, extinguishScanRadius);
            scanRadiusSq *= scanRadiusSq;

            foreach (IFireTarget fireTarget in BotRuntimeRegistry.ActiveFireTargets)
            {
                if (fireTarget == null || !fireTarget.IsBurning)
                {
                    continue;
                }

                if (!IsTargetLayerIncluded(fireTarget))
                {
                    continue;
                }

                float distanceSq = (fireTarget.GetWorldPosition() - worldPoint).sqrMagnitude;
                if (distanceSq > scanRadiusSq || !pointFires.Add(fireTarget))
                {
                    continue;
                }

                if (distanceSq < nearestFireDistanceSq)
                {
                    nearestFireDistanceSq = distanceSq;
                    nearestFire = fireTarget;
                }
            }

            foreach (IFireGroupTarget fireGroupCandidate in BotRuntimeRegistry.ActiveFireGroups)
            {
                if (fireGroupCandidate == null || !fireGroupCandidate.HasActiveFires)
                {
                    continue;
                }

                if (!IsTargetLayerIncluded(fireGroupCandidate))
                {
                    continue;
                }

                Vector3 nearestActiveFirePosition = fireGroupCandidate.GetClosestActiveFirePosition(worldPoint);
                float distanceSq = (nearestActiveFirePosition - worldPoint).sqrMagnitude;
                if (distanceSq > scanRadiusSq || !fireGroups.Add(fireGroupCandidate))
                {
                    continue;
                }

                if (distanceSq < nearestGroupDistanceSq)
                {
                    nearestGroupDistanceSq = distanceSq;
                    nearestGroup = fireGroupCandidate;
                }
            }

            if (logCommandSelection)
            {
                string nearestFireName = nearestFire is Component fireComponent && fireComponent != null
                    ? fireComponent.name
                    : "(none)";
                string nearestGroupName = nearestGroup is Component groupComponent && groupComponent != null
                    ? groupComponent.name
                    : "(none)";
                Debug.Log(
                    $"[FPSCommandSystem] Extinguish area scan at {worldPoint} radius {Mathf.Sqrt(scanRadiusSq):F2} -> pointFires={pointFires.Count}, fireGroups={fireGroups.Count}, nearestFire={nearestFireName}, nearestGroup={nearestGroupName}.",
                    this);
            }

            if (nearestGroup != null)
            {
                mode = BotExtinguishCommandMode.FireGroup;
                destination = nearestGroup.GetWorldCenter();
                fireGroupTarget = nearestGroup;
                return true;
            }

            if (nearestFire != null)
            {
                mode = BotExtinguishCommandMode.PointFire;
                destination = nearestFire.GetWorldPosition();
                pointFireTarget = nearestFire;
                return true;
            }

            return false;
        }

        private bool IsTargetLayerIncluded(object target)
        {
            if (extinguishScanMask.value == ~0)
            {
                return true;
            }

            Component component = target as Component;
            if (component == null || component.gameObject == null)
            {
                return true;
            }

            int layerBit = 1 << component.gameObject.layer;
            return (extinguishScanMask.value & layerBit) != 0;
        }

        private void SpawnExtinguishScanDebugSphere(Vector3 worldPoint)
        {
            if (!spawnExtinguishScanDebugSphere)
            {
                return;
            }

            SpawnCommandDebugSphere(
                "ExtinguishScanDebugSphere",
                worldPoint,
                Mathf.Max(0.1f, extinguishScanRadius),
                extinguishDebugSphereColor);
        }

        private void SpawnRescueDebugSphere(Vector3 worldPoint, global::BotCommandAgent botCommandAgent)
        {
            if (!spawnRescueDebugSphere)
            {
                return;
            }

            float radius = botCommandAgent != null
                ? Mathf.Max(0.1f, botCommandAgent.RescueSearchRadius)
                : Mathf.Max(0.1f, fallbackRescueDebugSphereRadius);

            SpawnCommandDebugSphere(
                "RescueDebugSphere",
                worldPoint,
                radius,
                rescueDebugSphereColor);
        }

        private void SpawnCommandDebugSphere(string debugName, Vector3 worldPoint, float radius, Color color)
        {
            GameObject debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            debugSphere.name = debugName;
            debugSphere.transform.position = worldPoint;
            debugSphere.transform.localScale = Vector3.one * radius * 2f;
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            debugSphere.layer = ignoreRaycastLayer >= 0 ? ignoreRaycastLayer : 0;

            Collider debugCollider = debugSphere.GetComponent<Collider>();
            if (debugCollider != null)
            {
                Destroy(debugCollider);
            }

            Renderer debugRenderer = debugSphere.GetComponent<Renderer>();
            if (debugRenderer != null)
            {
                debugRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                debugRenderer.receiveShadows = false;
                Material debugMaterial = CreateDebugSphereMaterial(color);
                if (debugMaterial != null)
                {
                    debugRenderer.sharedMaterial = debugMaterial;
                }
            }

            Destroy(debugSphere, Mathf.Max(0.1f, extinguishDebugSphereLifetime));
        }

        private Material CreateDebugSphereMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader);
            ApplyDebugColor(material, color);
            ConfigureTransparentMaterial(material);
            return material;
        }

        private static void ApplyDebugColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
        }

        private bool TryGetDestinationHit(out RaycastHit hit)
        {
            Ray ray = GetCenterRay();
            RaycastHit[] hits = Physics.RaycastAll(ray, destinationRayDistance, destinationMask, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                hit = default;
                DrawDebugRay(ray, destinationRayDistance, Color.magenta);
                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            hit = hits[0];
            DrawDebugRay(ray, hit.distance, Color.cyan);
            return true;
        }

        private bool TryGetContextualHits(out RaycastHit[] hits)
        {
            Ray ray = GetCenterRay();
            hits = Physics.RaycastAll(ray, destinationRayDistance, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return true;
        }

        private bool TryResolveContextualMoveCommand(RaycastHit[] hits, out BotCommandType commandType, out Vector3 destination, out IPickupable movePickupTarget)
        {
            commandType = BotCommandType.Move;
            movePickupTarget = null;
            destination = default;

            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < hits.Length; i++)
            {
                if (TryResolveContextualMoveCommandFromHit(hits[i], out commandType, out destination, out movePickupTarget))
                {
                    return true;
                }
            }

            destination = hits[0].point;
            return false;
        }

        private bool TryResolveContextualMoveCommandFromHit(RaycastHit hit, out BotCommandType commandType, out Vector3 destination, out IPickupable movePickupTarget)
        {
            commandType = BotCommandType.Move;
            destination = hit.point;
            movePickupTarget = null;

            if (TryFindInHitHierarchy(hit.collider, out IRescuableTarget rescuable) &&
                rescuable != null &&
                rescuable.NeedsRescue &&
                !rescuable.IsCarried)
            {
                commandType = BotCommandType.Rescue;
                destination = rescuable.GetWorldPosition();
                return true;
            }

            if (TryFindInHitHierarchy(hit.collider, out IFireTarget fireTarget) &&
                fireTarget != null &&
                fireTarget.IsBurning)
            {
                commandType = BotCommandType.Extinguish;
                destination = fireTarget.GetWorldPosition();
                return true;
            }

            if (TryFindInHitHierarchy(hit.collider, out IFireGroupTarget fireGroupTarget) &&
                fireGroupTarget != null &&
                fireGroupTarget.HasActiveFires)
            {
                commandType = BotCommandType.Extinguish;
                destination = fireGroupTarget.GetWorldCenter();
                return true;
            }

            if (TryFindInHitHierarchy(hit.collider, out IBotBreakableTarget breakableTarget) &&
                breakableTarget != null &&
                !breakableTarget.IsBroken)
            {
                destination = breakableTarget.GetWorldPosition();
                return true;
            }

            if (TryFindInHitHierarchy(hit.collider, out IPickupable pickupable) &&
                pickupable != null &&
                pickupable.Rigidbody != null)
            {
                movePickupTarget = pickupable;
                destination = pickupable.Rigidbody.transform.position;
                return true;
            }

            return false;
        }

        private static string GetHitDebugName(RaycastHit hit)
        {
            if (hit.collider == null)
            {
                return "(none)";
            }

            GameObject hitObject = hit.collider.gameObject;
            return hitObject != null ? hitObject.name : hit.collider.name;
        }

        private Ray GetCenterRay()
        {
            return viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        private static ICommandable FindCommandable(Collider collider, out GameObject targetObject)
        {
            targetObject = null;

            if (collider == null)
            {
                return null;
            }

            if (TryResolveCommandable(collider, out ICommandable direct, out targetObject))
            {
                return direct;
            }

            if (collider.attachedRigidbody != null &&
                TryResolveCommandable(collider.attachedRigidbody, out ICommandable rigidbodyOwner, out targetObject))
            {
                return rigidbodyOwner;
            }

            Transform parent = collider.transform.parent;
            while (parent != null)
            {
                if (TryResolveCommandable(parent, out ICommandable parentCommandable, out targetObject))
                {
                    return parentCommandable;
                }

                parent = parent.parent;
            }

            return null;
        }

        private static bool TryFindInHitHierarchy<T>(Collider collider, out T result) where T : class
        {
            result = null;
            if (collider == null)
            {
                return false;
            }

            if (TryResolveInterface(collider, out result))
            {
                return true;
            }

            if (collider.attachedRigidbody != null && TryResolveInterface(collider.attachedRigidbody, out result))
            {
                return true;
            }

            Transform current = collider.transform.parent;
            while (current != null)
            {
                if (TryResolveInterface(current, out result))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static ICommandable FindCommandable(Transform target, out GameObject targetObject)
        {
            targetObject = null;
            Transform current = target;
            while (current != null)
            {
                if (TryResolveCommandable(current, out ICommandable commandable, out targetObject))
                {
                    return commandable;
                }

                current = current.parent;
            }

            return null;
        }

        private static bool TryResolveCommandable(Component component, out ICommandable commandable, out GameObject targetObject)
        {
            targetObject = null;
            commandable = null;
            if (component == null)
            {
                return false;
            }

            commandable = component.GetComponent(typeof(ICommandable)) as ICommandable;
            if (commandable == null)
            {
                return false;
            }

            if (commandable is Component commandComponent)
            {
                targetObject = commandComponent.gameObject;
            }

            return true;
        }

        private static bool TryResolveInterface<T>(Component component, out T result) where T : class
        {
            result = null;
            if (component == null)
            {
                return false;
            }

            result = component.GetComponent(typeof(T)) as T;
            return result != null;
        }

        private void DrawDebugRay(Ray ray, float distance, Color color)
        {
            if (!drawDebugRay)
            {
                return;
            }

            Debug.DrawRay(ray.origin, ray.direction * distance, color);
        }

        private void UpdateTargetOutline(GameObject target)
        {
            GameObject outlineRoot = target != null ? ResolveSelectionOutlineRoot(target) : null;
            if (outlineRoot == currentOutlineRoot)
            {
                return;
            }

            ClearCurrentOutline();
            if (outlineRoot == null)
            {
                return;
            }

            currentOutlineRoot = outlineRoot;
            currentOutlineRenderers = outlineRoot.GetComponentsInChildren<Renderer>(true);
            currentOutlineRendererHadBit = new bool[currentOutlineRenderers.Length];

            uint selectedBit = 1u << selectedRenderingLayer;
            for (int i = 0; i < currentOutlineRenderers.Length; i++)
            {
                Renderer renderer = currentOutlineRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                currentOutlineRendererHadBit[i] = (renderer.renderingLayerMask & selectedBit) != 0;
                renderer.renderingLayerMask |= selectedBit;
            }
        }

        private void ClearCurrentOutline()
        {
            if (currentOutlineRenderers != null && currentOutlineRendererHadBit != null)
            {
                uint selectedBit = 1u << selectedRenderingLayer;
                int count = Mathf.Min(currentOutlineRenderers.Length, currentOutlineRendererHadBit.Length);
                for (int i = 0; i < count; i++)
                {
                    Renderer renderer = currentOutlineRenderers[i];
                    if (renderer == null || currentOutlineRendererHadBit[i])
                    {
                        continue;
                    }

                    renderer.renderingLayerMask &= ~selectedBit;
                }
            }

            currentOutlineRoot = null;
            currentOutlineRenderers = null;
            currentOutlineRendererHadBit = null;
        }

        private static GameObject ResolveSelectionOutlineRoot(GameObject target)
        {
            return target;
        }

        private void OnDrawGizmosSelected()
        {
            if (!IsAwaitingDestination || !hasPreviewPoint)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastPreviewPoint, 0.2f);
        }

        private void OnDisable()
        {
            ClearCurrentOutline();
        }

        private void OnGUI()
        {
            if (!showDebugOverlay)
            {
                return;
            }

            EnsureDebugGuiStyle();

            string hoveredName = GetTargetName(hoveredCommandTarget);
            string selectedName = GetTargetName(selectedCommandTarget);
            string status = IsAwaitingDestination ? "Awaiting destination" : "Idle";
            string debugText =
                $"Bot Hovered: {hoveredName}\n" +
                $"Bot Selected: {selectedName}\n" +
                $"Command State: {status}";

            Vector2 size = debugGuiStyle.CalcSize(new GUIContent(debugText));
            Rect rect = new Rect(
                debugOverlayOffset.x,
                debugOverlayOffset.y,
                Mathf.Max(260f, size.x + 16f),
                size.y + 12f);

            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f), debugText, debugGuiStyle);
            GUI.color = previousColor;
        }

        private void EnsureDebugGuiStyle()
        {
            if (debugGuiStyle != null)
            {
                return;
            }

            debugGuiStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                richText = false
            };
            debugGuiStyle.normal.textColor = Color.white;
            debugGuiStyle.hover.textColor = Color.white;
            debugGuiStyle.active.textColor = Color.white;
            debugGuiStyle.focused.textColor = Color.white;
            debugGuiStyle.onNormal.textColor = Color.white;
            debugGuiStyle.onHover.textColor = Color.white;
            debugGuiStyle.onActive.textColor = Color.white;
            debugGuiStyle.onFocused.textColor = Color.white;
        }

        private static string GetTargetName(GameObject target)
        {
            return target != null ? target.name : "(none)";
        }
    }
}
