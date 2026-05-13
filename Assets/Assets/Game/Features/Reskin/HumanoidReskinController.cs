using System.Collections.Generic;
using UnityEngine;

namespace FF3D.Features.Reskin
{
    /// <summary>
    /// Chịu trách nhiệm thay đổi skin (Reskin) của nhân vật humanoid.
    /// Phương pháp "Professional Reskin":
    /// - Giữ nguyên: Skeleton, Animator, Avatar, Gameplay, Hitbox.
    /// - Chỉ thay đổi: Mesh (SkinnedMeshRenderer), Material, Texture, và Accessories (MeshRenderer).
    /// </summary>
    public class HumanoidReskinController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Transform chứa toàn bộ khung xương gốc (Skeleton). Nếu để trống, script sẽ tự tìm qua Animator.")]
        [SerializeField] private Transform skeletonRoot;

        [Tooltip("Transform chứa các meshes hiện tại. Giúp tổ chức Hierarchy sạch sẽ và dễ dọn dẹp.")]
        [SerializeField] private Transform visualsContainer;

        [Header("Editor Testing")]
        [Tooltip("Gán ScriptableObject SkinDefinition vào đây và chọn 'Test Apply Skin' từ Context Menu để thử nghiệm.")]
        [SerializeField] private SkinDefinition testSkinDefinition;

        // Lưu trữ tham chiếu đến xương gốc dựa trên tên để map siêu nhanh
        private Dictionary<string, Transform> baseBoneMap;
        
        // Theo dõi các object visual (mesh, accessory) hiện tại để xoá khi đổi skin mới
        private List<GameObject> currentVisuals = new List<GameObject>();

        private void Awake()
        {
            InitializeBones();
        }

        /// <summary>
        /// Khởi tạo và cache lại bộ xương. Cần gọi nếu bạn spawn nhân vật động trong runtime.
        /// </summary>
        public void InitializeBones()
        {
            if (skeletonRoot == null)
            {
                // Thử tìm bone root (thường là parent của Hips)
                Animator anim = GetComponentInChildren<Animator>();
                if (anim != null && anim.isHuman)
                {
                    Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                    if (hips != null) skeletonRoot = hips.parent;
                }
            }

            if (skeletonRoot != null)
            {
                CacheBaseBones();
            }
            else
            {
                Debug.LogWarning("ReskinController: Không tìm thấy skeletonRoot. Vui lòng gán thủ công.", this);
            }
        }

        private void CacheBaseBones()
        {
            baseBoneMap = new Dictionary<string, Transform>();
            Transform[] allBones = skeletonRoot.GetComponentsInChildren<Transform>(true);
            foreach (var bone in allBones)
            {
                if (!baseBoneMap.ContainsKey(bone.name))
                {
                    baseBoneMap.Add(bone.name, bone);
                }
            }
        }

        /// <summary>
        /// Áp dụng một skin mới thông qua ScriptableObject SkinDefinition.
        /// </summary>
        /// <param name="skinData">Dữ liệu Skin cần apply.</param>
        public void ApplySkin(SkinDefinition skinData)
        {
            if (skinData == null || skinData.skinPrefab == null)
            {
                Debug.LogError("ReskinController: SkinData hoặc Skin Prefab bị null!");
                return;
            }

            if (baseBoneMap == null || baseBoneMap.Count == 0) 
            {
                InitializeBones();
            }

            ClearCurrentSkin();

            // 1. Spawn prefab của skin mới. 
            // Lưu ý: Lúc này nó đang là 1 bản sao dư thừa cả xương lẫn animator.
            GameObject skinInstance = Instantiate(skinData.skinPrefab, transform.position, transform.rotation);
            
            // 2. Quét và Map lại toàn bộ SkinnedMeshRenderer (Body, Clothes...)
            SkinnedMeshRenderer[] newSmrs = skinInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in newSmrs)
            {
                ProcessSkinnedMeshRenderer(smr);
            }

            // 3. Quét và Map lại các Accessories (Vũ khí, Mũ, Kính - thường là MeshRenderer tĩnh gắn vào xương)
            MeshFilter[] accessories = skinInstance.GetComponentsInChildren<MeshFilter>(true);
            foreach (var accessory in accessories)
            {
                // Bỏ qua nếu MeshFilter nằm trên object có SkinnedMeshRenderer (để tránh xử lý trùng)
                if (accessory.GetComponent<SkinnedMeshRenderer>() == null)
                {
                    ProcessAccessory(accessory.transform);
                }
            }

            // 4. Tiêu huỷ rác: Bộ xương, Animator thừa thãi từ bản instance của prefab mới
            Destroy(skinInstance);
        }

        /// <summary>
        /// Chuyển giao SkinnedMeshRenderer từ prefab mới sang bộ xương của character hiện tại.
        /// </summary>
        private void ProcessSkinnedMeshRenderer(SkinnedMeshRenderer smr)
        {
            // Map lại root bone
            if (smr.rootBone != null && baseBoneMap.TryGetValue(smr.rootBone.name, out Transform mappedRootBone))
            {
                smr.rootBone = mappedRootBone;
            }

            // Map lại mảng bones
            Transform[] newBones = new Transform[smr.bones.Length];
            for (int i = 0; i < smr.bones.Length; i++)
            {
                Transform originalBone = smr.bones[i];
                if (originalBone != null && baseBoneMap.TryGetValue(originalBone.name, out Transform mappedBone))
                {
                    newBones[i] = mappedBone;
                }
                else
                {
                    Debug.LogWarning($"[Reskin] Không tìm thấy bone '{originalBone?.name}' trong bộ xương gốc để gắn cho mesh '{smr.name}'", this);
                    newBones[i] = originalBone; // Cứ để nguyên làm fallback, dù có thể gây lỗi hiển thị
                }
            }
            smr.bones = newBones;

            // Chuyển object chứa SMR làm con của visualsContainer (hoặc root nhân vật) để dễ quản lý
            smr.transform.SetParent(visualsContainer != null ? visualsContainer : transform);
            currentVisuals.Add(smr.gameObject);
        }

        /// <summary>
        /// Chuyển giao các phụ kiện (Accessories) dạng mesh tĩnh từ prefab mới sang xương gốc.
        /// </summary>
        private void ProcessAccessory(Transform accessory)
        {
            // Tìm xem trong prefab mới, accessory này đang làm con của bone nào
            Transform originalParentBone = accessory.parent;
            
            if (originalParentBone != null && baseBoneMap.TryGetValue(originalParentBone.name, out Transform mappedParentBone))
            {
                // Nhấc sang bone có tên tương ứng ở bộ xương gốc
                // Giữ nguyên local position/rotation (worldPositionStays = false)
                accessory.SetParent(mappedParentBone, false);
                currentVisuals.Add(accessory.gameObject);
            }
            else
            {
                // Nếu nó không nằm trong xương (vd: nằm ở root), đẩy nó vào visualContainer
                accessory.SetParent(visualsContainer != null ? visualsContainer : transform, false);
                currentVisuals.Add(accessory.gameObject);
            }
        }

        /// <summary>
        /// Xoá hoàn toàn skin hiện tại trước khi mặc skin mới.
        /// </summary>
        public void ClearCurrentSkin()
        {
            foreach (var visual in currentVisuals)
            {
                if (visual != null)
                {
                    Destroy(visual);
                }
            }
            currentVisuals.Clear();
        }

        [ContextMenu("Test Apply Skin")]
        private void DoTestApplySkin()
        {
            if (testSkinDefinition != null)
            {
                ApplySkin(testSkinDefinition);
            }
            else
            {
                Debug.LogWarning("Chưa gán 'Test Skin Definition' trong inspector.");
            }
        }
    }
}
