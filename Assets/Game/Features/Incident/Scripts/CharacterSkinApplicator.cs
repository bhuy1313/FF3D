using UnityEngine;

[DisallowMultipleComponent]
public class CharacterSkinApplicator : MonoBehaviour
{
    [Header("Skin")]
    [SerializeField] private CharacterSkinData skinData;
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool applyInEditorOnValidate = true;

    [Header("Renderers")]
    [SerializeField] private SkinnedMeshRenderer bodyRenderer;
    [SerializeField] private SkinnedMeshRenderer hairRenderer;
    [SerializeField] private SkinnedMeshRenderer eyelashesRenderer;
    [SerializeField] private SkinnedMeshRenderer pantsRenderer;
    [SerializeField] private SkinnedMeshRenderer shoesRenderer;
    [SerializeField] private SkinnedMeshRenderer suitRenderer;

    public CharacterSkinData SkinData
    {
        get => skinData;
        set => skinData = value;
    }

    private void Awake()
    {
        if (applyOnAwake)
        {
            ApplySkin();
        }
    }

    private void OnValidate()
    {
        if (!applyInEditorOnValidate)
        {
            return;
        }

        ApplySkin();
    }

    [ContextMenu("Apply Skin")]
    public void ApplySkin()
    {
        if (skinData == null)
        {
            return;
        }

        ApplyMesh(bodyRenderer, skinData.body);
        ApplyMesh(hairRenderer, skinData.hair);
        ApplyMesh(eyelashesRenderer, skinData.eyelashes);
        ApplyMesh(pantsRenderer, skinData.pants);
        ApplyMesh(shoesRenderer, skinData.shoes);
        ApplyMesh(suitRenderer, skinData.suit);

        ApplySingleMaterial(bodyRenderer, skinData.bodyMaterial);
        ApplySingleMaterial(hairRenderer, skinData.hairMaterial);
    }

    private static void ApplyMesh(SkinnedMeshRenderer targetRenderer, Mesh mesh)
    {
        if (targetRenderer == null || mesh == null)
        {
            return;
        }

        targetRenderer.sharedMesh = mesh;
    }

    private static void ApplySingleMaterial(SkinnedMeshRenderer targetRenderer, Material material)
    {
        if (targetRenderer == null || material == null)
        {
            return;
        }

        targetRenderer.sharedMaterial = material;
    }
}
