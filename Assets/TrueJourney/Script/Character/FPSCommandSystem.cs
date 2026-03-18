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
        [SerializeField] private float destinationRayDistance = 200f;
        [SerializeField] private LayerMask destinationMask = ~0;

        [Header("Debug")]
        [SerializeField] private bool drawDebugRay;
        [SerializeField] private bool showDebugOverlay = true;
        [SerializeField] private bool logCommandSelection = true;
        [SerializeField] private Vector2 debugOverlayOffset = new Vector2(16f, 16f);

        private readonly BotCommandState commandState = new BotCommandState();
        private GUIStyle debugGuiStyle;

        private ICommandable hoveredCommandable;
        private GameObject hoveredCommandTarget;
        [SerializeField] private GameObject selectedCommandTarget;
        private GameObject currentOutlineRoot;
        private Renderer[] currentOutlineRenderers;
        private bool[] currentOutlineRendererHadBit;
        private Vector3 lastPreviewPoint;
        private bool hasPreviewPoint;

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
        }

        private void Update()
        {
            if (viewCamera == null)
            {
                return;
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

            if (!commandState.IsAwaitingTarget)
            {
                if (Input.GetKeyDown(moveCommandKey))
                {
                    TryStartMoveCommand();
                }

                return;
            }

            UpdatePreviewPoint();

            if (Input.GetMouseButtonDown(0))
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

        private void TryStartMoveCommand()
        {
            if (hoveredCommandable == null)
            {
                return;
            }

            if (!commandState.TryBegin(hoveredCommandable, BotCommandType.Move))
            {
                return;
            }

            selectedCommandTarget = hoveredCommandTarget;
            UpdateTargetOutline(selectedCommandTarget);
            UpdatePreviewPoint();

            if (logCommandSelection)
            {
                Debug.Log($"[FPSCommandSystem] Selected bot '{GetTargetName(selectedCommandTarget)}' for command '{BotCommandType.Move}'.", this);
            }
        }

        private void TryConfirmPendingCommand()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (!TryGetDestinationPoint(out Vector3 destination))
            {
                return;
            }

            if (commandState.TryConfirm(destination))
            {
                if (logCommandSelection)
                {
                    Debug.Log($"[FPSCommandSystem] Issued '{BotCommandType.Move}' to '{GetTargetName(selectedCommandTarget)}' at {destination}.", this);
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
            if (logCommandSelection && selectedCommandTarget != null)
            {
                Debug.Log($"[FPSCommandSystem] Cancelled command for '{GetTargetName(selectedCommandTarget)}'.", this);
            }

            commandState.Cancel();
            selectedCommandTarget = null;
            UpdateTargetOutline(null);
            hasPreviewPoint = false;
        }

        private void UpdatePreviewPoint()
        {
            hasPreviewPoint = TryGetDestinationPoint(out lastPreviewPoint);
        }

        private bool TryGetDestinationPoint(out Vector3 destination)
        {
            Ray ray = GetCenterRay();
            if (Physics.Raycast(ray, out RaycastHit hit, destinationRayDistance, destinationMask, QueryTriggerInteraction.Ignore))
            {
                destination = hit.point;
                DrawDebugRay(ray, hit.distance, Color.cyan);
                return true;
            }

            destination = default;
            DrawDebugRay(ray, destinationRayDistance, Color.magenta);
            return false;
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
            GUI.color = previousColor;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f), debugText, debugGuiStyle);
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
