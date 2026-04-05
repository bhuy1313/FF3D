using UnityEngine;

public interface IBurnable
{
    Fire FireSource { get; }
    float BurnProgress { get; }
    bool HasDeformableMesh { get; }
}
