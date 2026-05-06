using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class HudTabToggle : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Phím dùng để bật/tắt (Mặc định: Tab)")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Tooltip("Trạng thái hiển thị mặc định khi mới chạy game")]
    [SerializeField] private bool defaultVisible = false;

    [Header("Animator Setup")]
    [Tooltip("Kéo Animator của HubV2_Detail vào đây")]
    [SerializeField] private Animator panelAnimator;

    [Tooltip("Tên trigger khi mở")]
    [SerializeField] private string openTrigger = "Open";

    [Tooltip("Tên trigger khi đóng")]
    [SerializeField] private string closeTrigger = "Close";
    
    [Tooltip("Có bắn Trigger ngay khi bắt đầu game không? (Tắt đi nếu đã có Animation tổng tự chạy)")]
    [SerializeField] private bool fireTriggerOnStart = false;

    [Header("Runtime State")]
    [Tooltip("Trạng thái hiện tại của DetailGroup (Chỉ nên xem, không nên sửa tay)")]
    [SerializeField] private bool isVisible;
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private PlayerInput playerInput;
#endif

    /// <summary>
    /// Cho biết DetailGroup hiện đang được bật hay tắt.
    /// </summary>
    public bool IsDetailVisible => isVisible;

    private void Awake()
    {
        isVisible = defaultVisible;
        
        // Khởi tạo trạng thái ban đầu cho Animator
        if (panelAnimator != null && fireTriggerOnStart)
        {
            if (isVisible)
            {
                panelAnimator.SetTrigger(openTrigger);
            }
            else
            {
                panelAnimator.SetTrigger(closeTrigger);
            }
        }
    }

    private void Update()
    {
        if (WasTogglePressed())
        {
            if (panelAnimator == null)
            {
                Debug.LogWarning("[HudTabToggle] Chưa gán Animator cho panelAnimator!");
                return;
            }

            // Đảo ngược trạng thái logic (bật thành tắt, tắt thành bật)
            isVisible = !isVisible;

            if (isVisible)
            {
                // Muốn MỞ: Đảm bảo GameObject đang được bật trước khi chạy Animation
                if (!panelAnimator.gameObject.activeSelf)
                {
                    panelAnimator.gameObject.SetActive(true);
                }
                panelAnimator.SetTrigger(openTrigger);
            }
            else
            {
                // Muốn ĐÓNG: Chỉ bắn Trigger Close, không SetActive(false) ngay lập tức
                panelAnimator.SetTrigger(closeTrigger);
            }
        }
    }

    private bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (playerInput == null)
        {
            playerInput = FindAnyObjectByType<PlayerInput>();
        }

        if (playerInput != null && playerInput.actions != null)
        {
            InputAction action = playerInput.actions.FindAction("ToggleMissionHud", throwIfNotFound: false);
            if (action != null && action.WasPressedThisFrame())
            {
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(toggleKey);
#else
        return false;
#endif
    }
}
