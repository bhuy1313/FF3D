using UnityEngine;

namespace FF3D.Features.Reskin
{
    /// <summary>
    /// ScriptableObject chứa toàn bộ thông tin của một skin.
    /// Giúp quản lý theo hướng Data-Driven: dễ dàng tạo mới, cấu hình và liên kết với UI Menu.
    /// </summary>
    [CreateAssetMenu(fileName = "New Skin Definition", menuName = "FF3D/Reskin/Skin Definition", order = 1)]
    public class SkinDefinition : ScriptableObject
    {
        [Header("Skin Info")]
        [Tooltip("ID duy nhất cho skin (dùng để lưu file save, load data)")]
        public string id = "skin_default";
        
        [Tooltip("Tên hiển thị trên UI")]
        public string displayName = "Default Skin";
        
        [Tooltip("Mô tả ngắn hiển thị trên UI")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Icon hiển thị trên UI Shop/Inventory")]
        public Sprite icon;

        [Header("Visual Assets")]
        [Tooltip("Prefab chứa model/mesh thay thế (phải có chung cấu trúc xương với nhân vật gốc)")]
        public GameObject skinPrefab;
        
        // (Optional) Bạn có thể mở rộng thêm ở đây:
        // public Material[] overrideMaterials;
        // public AudioClip customVoice;
        // public ParticleSystem customSpawnEffect;
    }
}
