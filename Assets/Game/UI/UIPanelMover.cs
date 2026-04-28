using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro; // Thêm thư viện TextMeshPro

namespace FF3D.UI
{
    public class UIPanelMover : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Kéo UI element (RectTransform) cần di chuyển vào đây")]
        [SerializeField] private RectTransform targetRect;
        
        [Tooltip("Kéo Button để kích hoạt vào đây (Hoặc có thể gọi hàm TogglePanel từ OnClick của Button)")]
        [SerializeField] private Button triggerButton;

        [Tooltip("Kéo TextMeshPro (nằm trong Button) vào đây để đổi chữ")]
        [SerializeField] private TMP_Text buttonText;

        [Header("Target Offsets (Mở rộng)")]
        [SerializeField] private float targetTop = 80f;
        [SerializeField] private float targetBottom = -80f;
        
        [Header("Original Offsets (Thu gọn)")]
        [SerializeField] private float originalTop = 0f;
        [SerializeField] private float originalBottom = 0f;

        [Header("Button Text Settings")]
        [SerializeField] private string expandedText = "^";
        [SerializeField] private string collapsedText = "v";

        [Header("Animation Settings")]
        [SerializeField] private float duration = 0.5f;
        [SerializeField] private Ease easeType = Ease.OutCubic;

        // Lưu trạng thái hiện tại (Đang mở hay đang đóng)
        private bool isExpanded = false;

        private void Start()
        {
            // Thiết lập text hiển thị ban đầu là "v"
            UpdateButtonText();

            // Lắng nghe sự kiện click của button nếu được gán từ Inspector
            if (triggerButton != null)
            {
                triggerButton.onClick.AddListener(TogglePanel);
            }
        }

        private void OnDestroy()
        {
            if (triggerButton != null)
            {
                triggerButton.onClick.RemoveListener(TogglePanel);
            }
        }

        /// <summary>
        /// Hàm này xử lý logic bật/tắt (Toggle) mỗi khi click
        /// </summary>
        public void TogglePanel()
        {
            if (targetRect == null)
            {
                Debug.LogWarning("Target RectTransform chưa được gán trong Inspector!", this);
                return;
            }

            // Đảo ngược trạng thái
            isExpanded = !isExpanded;

            // Xác định đích đến dựa trên trạng thái (Mở thì dùng target, Đóng thì dùng original)
            float targetTopValue = isExpanded ? targetTop : originalTop;
            float targetBottomValue = isExpanded ? targetBottom : originalBottom;

            // Trong RectTransform của Unity UI:
            // Top tương đương với giá trị (-offsetMax.y)
            // Bottom tương đương với giá trị (offsetMin.y)
            Vector2 endOffsetMax = new Vector2(targetRect.offsetMax.x, -targetTopValue);
            Vector2 endOffsetMin = new Vector2(targetRect.offsetMin.x, targetBottomValue);

            // Dùng DOTween.To để animate mượt mà cho 2 thông số offsetMin và offsetMax
            DOTween.To(() => targetRect.offsetMax, x => targetRect.offsetMax = x, endOffsetMax, duration)
                .SetEase(easeType);

            DOTween.To(() => targetRect.offsetMin, x => targetRect.offsetMin = x, endOffsetMin, duration)
                .SetEase(easeType);

            // Cập nhật lại Text cho nút bấm
            UpdateButtonText();
        }

        private void UpdateButtonText()
        {
            if (buttonText != null)
            {
                buttonText.text = isExpanded ? expandedText : collapsedText;
            }
        }
    }
}
