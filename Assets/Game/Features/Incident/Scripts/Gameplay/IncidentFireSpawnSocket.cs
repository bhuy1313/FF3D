using UnityEngine;

[DisallowMultipleComponent]
public class IncidentFireSpawnSocket : MonoBehaviour
{
    [Header("Selection")]
    [SerializeField] private bool canSpawnPrimary = true;
    [SerializeField] private bool canSpawnSecondary = true;
    [SerializeField] [Min(1)] private int selectionWeight = 1;

    [Header("Placement")]
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private bool useSocketRotation = true;

    public bool CanSpawnPrimary => canSpawnPrimary;
    public bool CanSpawnSecondary => canSpawnSecondary;
    public int SelectionWeight => Mathf.Max(1, selectionWeight);
    public Vector3 WorldPosition => transform.TransformPoint(positionOffset);
    public Quaternion WorldRotation => useSocketRotation ? transform.rotation : Quaternion.identity;
}
