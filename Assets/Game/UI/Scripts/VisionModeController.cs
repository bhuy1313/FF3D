using UnityEngine;
using UnityEngine.Rendering; // Cần thiết để dùng Volume

public class VisionModeController : MonoBehaviour
{
    [Header("Volume Setup")]
    [SerializeField] private Volume visionVolume;
    
    [Header("Presets (Volume Profiles)")]
    [Tooltip("Danh sách các cấu hình Vision Mode (Kéo thả các Volume Profile vào đây).")]
    [SerializeField] private VolumeProfile[] visionPresets;
    
    [Tooltip("Chỉ số của Preset đang được sử dụng (0, 1, 2...).")]
    [SerializeField] private int activePresetIndex = 0;

    [Header("Settings")]
    [SerializeField] private float transitionSpeed = 5f;

    private bool isVisionActive = false;
    private float targetWeight = 0f;
    private int lastPresetIndex = -1;

    private void Start()
    {
        ApplyPreset(activePresetIndex);
    }

    private void Update()
    {
        // Tự động cập nhật nếu bạn đổi Preset Index trong Inspector lúc đang Play
        if (activePresetIndex != lastPresetIndex)
        {
            ApplyPreset(activePresetIndex);
        }

        // Bấm phím Tab để đổi nhanh qua lại giữa các Preset (chỉ khi đang bật Vision Mode)
        if (Input.GetKeyDown(KeyCode.Tab) && isVisionActive)
        {
            NextPreset();
        }

        // Bấm phím V để bật/tắt tầm nhìn
        if (Input.GetKeyDown(KeyCode.V))
        {
            isVisionActive = !isVisionActive;
            targetWeight = isVisionActive ? 1f : 0f;
            Debug.Log($"Vision Mode {(isVisionActive ? "Activated" : "Deactivated")} | Preset Index: {activePresetIndex}");
            
            // Ở đây bạn có thể gọi thêm logic bật Outline cho các mục tiêu
        }

        // Chuyển đổi mượt mà
        if (visionVolume != null)
        {
            visionVolume.weight = Mathf.Lerp(visionVolume.weight, targetWeight, Time.deltaTime * transitionSpeed);
        }
    }

    public void NextPreset()
    {
        if (visionPresets == null || visionPresets.Length == 0) return;
        
        activePresetIndex = (activePresetIndex + 1) % visionPresets.Length;
        ApplyPreset(activePresetIndex);
        Debug.Log("Switched to Vision Preset: " + activePresetIndex + " (" + visionVolume.profile.name + ")");
    }

    private void ApplyPreset(int index)
    {
        lastPresetIndex = index;

        if (visionVolume == null || visionPresets == null || visionPresets.Length == 0) 
            return;

        // Giới hạn index trong khoảng hợp lệ
        index = Mathf.Clamp(index, 0, visionPresets.Length - 1);
        activePresetIndex = index;

        if (visionPresets[index] != null)
        {
            visionVolume.profile = visionPresets[index];
        }
    }
}