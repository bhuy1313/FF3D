using UnityEngine;

[CreateAssetMenu(menuName = "Character/Skin Data")]
public class CharacterSkinData : ScriptableObject
{
    [Header("Shared Materials")]
    public Material bodyMaterial;

    public Material hairMaterial;

    [Header("Meshes")]
    public Mesh body;

    public Mesh hair;

    public Mesh eyelashes;

    public Mesh pants;

    public Mesh shirt;

    public Mesh collar;
    
    public Mesh shoes;

    public Mesh belt;

    public Mesh suit;

    public Mesh tie;

}