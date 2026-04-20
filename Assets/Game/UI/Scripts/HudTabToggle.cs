using UnityEngine;

public class HudTabToggle : MonoBehaviour
{
    [Header("Targets to Toggle")]
    [Tooltip("Kéo GameObject thứ nhất (ví dụ: HubV2) vào đây")]
    [SerializeField] private GameObject target1;

    [Tooltip("Kéo GameObject thứ hai vào đây (nếu có)")]
    [SerializeField] private GameObject target2;

    [Header("Settings")]
    [Tooltip("Phím dùng để bật/tắt (Mặc định: Tab)")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Tooltip("Trạng thái hiển thị mặc định khi mới chạy game")]
    [SerializeField] private bool defaultVisible = false;

    private bool isVisible;

    private void Awake()
    {
        // Thiết lập trạng thái ban đầu
        isVisible = defaultVisible;
        SetTargetsActive(isVisible);
    }

    private void Update()
    {
        // Kiểm tra xem người chơi có nhấn phím Tab (hoặc phím đã cài) không
        if (Input.GetKeyDown(toggleKey))
        {
            // Đảo ngược trạng thái: Đang hiện -> Ẩn, Đang ẩn -> Hiện
            isVisible = !isVisible;
            SetTargetsActive(isVisible);
        }
    }

    private void SetTargetsActive(bool active)
    {
        if (target1 != null)
        {
            target1.SetActive(active);
        }

        if (target2 != null)
        {
            target2.SetActive(active);
        }
    }
}
