using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FireHoseConnectionSystem : MonoBehaviour
{
    [Header("Registry")]
    [SerializeField] private List<FireHoseConnectionEndpoint> endpoints = new List<FireHoseConnectionEndpoint>();

    [System.Serializable]
    private class ActiveRigRecord
    {
        public FireHoseRig rig;
        public FireTruckHosePickupPoint sourcePoint;
        public FireHoseConnectionPoint targetPoint;
    }

    [Header("Runtime")]
    [SerializeField] private List<ActiveRigRecord> activeRigs = new List<ActiveRigRecord>();

    public void RegisterEndpoint(FireHoseConnectionEndpoint endpoint)
    {
        if (endpoint == null || endpoints.Contains(endpoint))
        {
            return;
        }

        endpoints.Add(endpoint);
    }

    public void UnregisterEndpoint(FireHoseConnectionEndpoint endpoint)
    {
        if (endpoint == null)
        {
            return;
        }

        endpoints.Remove(endpoint);
    }

    public bool TryDeployRig(FireTruckHosePickupPoint sourcePoint, GameObject interactor, Vector3 headSpawnPosition, Vector3 startKnotPosition, Vector3 startKnotNormal)
    {
        if (sourcePoint == null || interactor == null)
        {
            return false;
        }

        if (sourcePoint.IsSingleUse && FindRecordBySource(sourcePoint) != null)
        {
            return false;
        }

        FireHoseRig rig = CreateRuntimeRig(sourcePoint);
        if (rig == null)
        {
            return false;
        }

        rig.PrepareForDeploy(sourcePoint, headSpawnPosition, startKnotPosition, startKnotNormal);

        activeRigs.Add(new ActiveRigRecord
        {
            rig = rig,
            sourcePoint = sourcePoint,
            targetPoint = null
        });

        return true;
    }

    private FireHoseRig CreateRuntimeRig(FireTruckHosePickupPoint sourcePoint)
    {
        if (sourcePoint.HoseBodyPrefab == null)
        {
            return null;
        }

        Transform parent = sourcePoint.ResolveRigParent();

        GameObject root = new GameObject("FireHoseRig_Runtime");
        root.transform.SetParent(parent, false);

        FireHoseAssembly assembly = root.AddComponent<FireHoseAssembly>();
        FireHoseRig rig = root.AddComponent<FireHoseRig>();

        GameObject hoseBody = Instantiate(sourcePoint.HoseBodyPrefab, root.transform);
        hoseBody.name = sourcePoint.HoseBodyPrefab.name;

        GameObject headObject = CreateRuntimeHead(root.transform);
        headObject.name = "FireHose_Head";

        FireHoseDeployable deployable = hoseBody.GetComponentInChildren<FireHoseDeployable>(true);
        if (deployable != null)
        {
            deployable.head = headObject.transform;
        }

        assembly.ConfigureRig(rig);

        return rig;
    }

    private static GameObject CreateRuntimeHead(Transform parent)
    {
        GameObject headObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        headObject.transform.SetParent(parent, false);
        headObject.transform.localScale = Vector3.one * 0.3f;
        headObject.transform.localRotation = Quaternion.identity;
        headObject.layer = 14;

        Rigidbody body = headObject.AddComponent<Rigidbody>();
        body.mass = 12f;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        headObject.AddComponent<FireHoseHeadPickup>();
        return headObject;
    }

    public bool TryConnectHeldRigToHydrant(FireHoseConnectionPoint connectionPoint, GameObject interactor)
    {
        if (connectionPoint == null || interactor == null)
        {
            return false;
        }

        FireHoseRig rig = ResolveHeldRig(interactor);
        if (rig == null)
        {
            return false;
        }

        if (!rig.TryConnectHydrant(connectionPoint))
        {
            return false;
        }

        ActiveRigRecord record = FindRecordByRig(rig);
        if (record != null)
        {
            record.targetPoint = connectionPoint;
        }

        return true;
    }

    private FireHoseRig ResolveHeldRig(GameObject interactor)
    {
        if (!interactor.TryGetComponent(out FPSInventorySystem inventory))
        {
            return null;
        }

        GameObject heldObject = inventory.HeldObject;
        if (heldObject == null)
        {
            return null;
        }

        FireHoseHeadPickup headPickup =
            heldObject.GetComponent<FireHoseHeadPickup>() ??
            heldObject.GetComponentInParent<FireHoseHeadPickup>();
        if (headPickup == null)
        {
            return null;
        }

        return headPickup.GetComponentInParent<FireHoseRig>();
    }

    private ActiveRigRecord FindRecordBySource(FireTruckHosePickupPoint sourcePoint)
    {
        for (int i = 0; i < activeRigs.Count; i++)
        {
            ActiveRigRecord record = activeRigs[i];
            if (record != null && record.sourcePoint == sourcePoint)
            {
                return record;
            }
        }

        return null;
    }

    private ActiveRigRecord FindRecordByRig(FireHoseRig rig)
    {
        for (int i = 0; i < activeRigs.Count; i++)
        {
            ActiveRigRecord record = activeRigs[i];
            if (record != null && record.rig == rig)
            {
                return record;
            }
        }

        return null;
    }
}
