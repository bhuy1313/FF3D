using UnityEngine;

public interface IBurnable
{
    float CurrentFireContactDamagePerSecond { get; }
    float BurnProgress { get; }
    bool HasDeformableMesh { get; }
}
