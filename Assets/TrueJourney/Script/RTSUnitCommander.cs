using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class RTSUnitCommander : MonoBehaviour
{
    [Header("Masks")]
    [SerializeField] private LayerMask groundMask = ~0;        // layer mặt đất
    [SerializeField] private LayerMask interactableMask = ~0;  // layer vật thể tương tác

    [Header("Pick & Move")]
    [SerializeField] private float rayMaxDistance = 1500f;
    [SerializeField] private float clickEps = 0.01f;

    private Camera cam;
    private Vector3 _lastRightClickPos = new Vector3(float.PositiveInfinity, 0, 0);
    private bool _hasPrevClick;
    private GameObject _lastTarget;

    // Sự kiện để CommandHandler bắt
    public event Action<Vector3> OnMoveCommandIssued;
    public event Action<GameObject> OnPickupCommandIssued;
    public event Action OnForceStop;

    private void Awake()
    {
        cam = Camera.main;
        if (!cam) Debug.LogWarning("[RTSUnitCommander] Camera.main is null.");
    }

    private void Update()
    {
        if (!cam) return;

        // Bỏ qua nếu trỏ chuột đang trên UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Chuột phải?
        if (!Input.GetMouseButtonDown(1))
            return;

        // Ray từ màn hình
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // 1) Ưu tiên xem có đụng vật thể tương tác không
        GameObject hitTarget = null;
        if (Physics.Raycast(ray, out RaycastHit hitTargetRH, rayMaxDistance, interactableMask, QueryTriggerInteraction.Ignore))
        {
            if (hitTargetRH.collider) hitTarget = hitTargetRH.collider.gameObject;
        }

        // 2) Bắt buộc phải có hit mặt đất để lấy vị trí đích
        if (!Physics.Raycast(ray, out RaycastHit hitGround, rayMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
            return;

        Vector3 pos = hitGround.point;

        // Lọc click trùng
        float epsSqr = clickEps * clickEps;
        bool positionChanged = !_hasPrevClick || (pos - _lastRightClickPos).sqrMagnitude > epsSqr;
        bool targetChanged = hitTarget != _lastTarget;

        if (!positionChanged && !targetChanged)
            return;

        // Phát sự kiện
        OnForceStop?.Invoke(); // cho unit dừng hành động cũ

        if (hitTarget != null)
            OnPickupCommandIssued?.Invoke(hitTarget); // hoặc Attack/Interact tuỳ game
        else
            OnMoveCommandIssued?.Invoke(pos);

        // Cập nhật trạng thái trước
        _lastRightClickPos = pos;
        _lastTarget = hitTarget;
        _hasPrevClick = true;

        Debug.DrawRay(ray.origin, ray.direction * 10f, Color.yellow, 0.2f);
    }
}
