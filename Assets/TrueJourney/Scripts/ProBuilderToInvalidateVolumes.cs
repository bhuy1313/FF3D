#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ProBuilderToInvalidateVolumes
{
    [MenuItem("Tools/APV/Create Invalidate Volumes From Selection")]
    private static void CreateVolumes()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            MeshFilter meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            Bounds worldBounds = CalculateWorldBounds(meshFilter);

            GameObject volumeObject = new GameObject(go.name + "_InvalidateProbes");
            Undo.RegisterCreatedObjectUndo(volumeObject, "Create Probe Adjustment Volume");

            if (go.scene.IsValid() && volumeObject.scene != go.scene)
            {
                SceneManager.MoveGameObjectToScene(volumeObject, go.scene);
            }

            volumeObject.transform.SetPositionAndRotation(worldBounds.center, Quaternion.identity);
            volumeObject.transform.localScale = Vector3.one;

            ProbeAdjustmentVolume probeAdjustmentVolume = Undo.AddComponent<ProbeAdjustmentVolume>(volumeObject);
            probeAdjustmentVolume.shape = ProbeAdjustmentVolume.Shape.Box;
            probeAdjustmentVolume.mode = ProbeAdjustmentVolume.Mode.InvalidateProbes;
            probeAdjustmentVolume.size = worldBounds.size;
        }
    }

    [MenuItem("Tools/APV/Create Invalidate Volumes From Selection", true)]
    private static bool CanCreateVolumes()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            MeshFilter meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                return true;
            }
        }

        return false;
    }

    private static Bounds CalculateWorldBounds(MeshFilter meshFilter)
    {
        Bounds localBounds = meshFilter.sharedMesh.bounds;
        Transform targetTransform = meshFilter.transform;

        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;

        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        Bounds worldBounds = new Bounds(targetTransform.TransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
        {
            worldBounds.Encapsulate(targetTransform.TransformPoint(corners[i]));
        }

        return worldBounds;
    }
}
#endif
