using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FireHoseConnectionSystemLite : MonoBehaviour
{
    [System.Serializable]
    private class ActiveRigRecord
    {
        public FireHoseRig rig;
        public FireTruckHosePickupPointLite sourcePoint;
        public FireHoseConnectionPoint targetPoint;
    }

    private struct SpawnedLiteHead
    {
        public Transform Head;
        public Transform RotationAnchor;
        public FireHoseHeadPickup Pickup;
    }

    [Header("Runtime")]
    [SerializeField] private List<ActiveRigRecord> activeRigs = new List<ActiveRigRecord>();

    public bool TryDeployRig(
        FireTruckHosePickupPointLite sourcePoint,
        GameObject interactor,
        Vector3 headSpawnPosition,
        Vector3 startKnotPosition,
        Vector3 startKnotNormal,
        Vector3 tailEndPosition)
    {
        if (sourcePoint == null || interactor == null)
        {
            return false;
        }

        if (sourcePoint.IsSingleUse && FindRecordBySource(sourcePoint) != null)
        {
            return false;
        }

        FireHoseRig rig = CreateRuntimeRig(sourcePoint, headSpawnPosition, startKnotPosition, startKnotNormal, tailEndPosition);
        if (rig == null)
        {
            return false;
        }

        activeRigs.Add(new ActiveRigRecord
        {
            rig = rig,
            sourcePoint = sourcePoint,
            targetPoint = null
        });

        return true;
    }

    private FireHoseRig CreateRuntimeRig(
        FireTruckHosePickupPointLite sourcePoint,
        Vector3 headSpawnPosition,
        Vector3 rootWorldPosition,
        Vector3 startKnotNormal,
        Vector3 tailEndPosition)
    {
        if (sourcePoint.HoseBodyPrefab == null)
        {
            return null;
        }

        Transform parent = sourcePoint.ResolveRigParent();

        GameObject root = new GameObject("FireHoseRig_Runtime_Lite");
        root.transform.SetParent(parent, false);
        root.transform.position = rootWorldPosition;
        root.transform.rotation = Quaternion.identity;

        FireHoseAssembly assembly = root.AddComponent<FireHoseAssembly>();
        FireHoseRig rig = root.AddComponent<FireHoseRig>();

        GameObject hoseBody = Instantiate(sourcePoint.HoseBodyPrefab, root.transform);
        hoseBody.name = sourcePoint.HoseBodyPrefab.name;

        FireHoseDeployable deployable = hoseBody.GetComponentInChildren<FireHoseDeployable>(true);
        SpawnedLiteHead spawnedLiteHead = SpawnLiteHead(sourcePoint, root.transform, headSpawnPosition, startKnotNormal);
        if (spawnedLiteHead.Head == null || spawnedLiteHead.RotationAnchor == null || spawnedLiteHead.Pickup == null)
        {
            Destroy(root);
            return null;
        }

        spawnedLiteHead.Head.name = "FireHose_ConnectorHead";
        spawnedLiteHead.Head.gameObject.layer = sourcePoint.HeadLayer;

        if (deployable != null)
        {
            deployable.head = spawnedLiteHead.RotationAnchor;
            deployable.groundMask = sourcePoint.GroundMask;
        }

        assembly.ConfigureLiteOwner(sourcePoint);
        assembly.SetHeadSnapHeightOffset(sourcePoint.HeadSnapHeightOffset);
        assembly.ConfigureRig(rig);
        rig.ConfigureLiteSourcePoint(sourcePoint);
        spawnedLiteHead.Pickup.ConfigureAssembly(assembly);
        rig.PrepareForDeploy(null, spawnedLiteHead.RotationAnchor.position, rootWorldPosition, startKnotNormal, tailEndPosition);
        return rig;
    }

    private static SpawnedLiteHead SpawnLiteHead(
        FireTruckHosePickupPointLite sourcePoint,
        Transform parent,
        Vector3 headSpawnPosition,
        Vector3 startKnotNormal)
    {
        if (sourcePoint == null || sourcePoint.HeadPrefab == null)
        {
            return default;
        }

        Vector3 up = startKnotNormal.sqrMagnitude > 0.0001f ? startKnotNormal.normalized : Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(sourcePoint.transform.forward, up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(sourcePoint.transform.right, up);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.Cross(up, Vector3.right);
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        Quaternion headRotation = Quaternion.LookRotation(forward.normalized, up);
        GameObject headObject = Instantiate(sourcePoint.HeadPrefab, headSpawnPosition, Quaternion.identity, parent);
        headObject.name = "FireHose_ConnectorHead";

        FireHoseHeadPickup pickup = headObject.GetComponent<FireHoseHeadPickup>();
        if (pickup == null)
        {
            pickup = headObject.AddComponent<FireHoseHeadPickup>();
        }

        Transform rotationAnchor = pickup.RotationAnchor;
        if (rotationAnchor != null)
        {
            rotationAnchor.SetPositionAndRotation(headSpawnPosition, headRotation);
        }

        return new SpawnedLiteHead
        {
            Head = headObject.transform,
            RotationAnchor = rotationAnchor,
            Pickup = pickup
        };
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

        return TryConnectRigToHydrant(connectionPoint, rig, interactor);
    }

    public bool TryConnectRigToHydrant(FireHoseConnectionPoint connectionPoint, FireHoseRig rig, GameObject interactor)
    {
        if (connectionPoint == null || rig == null || interactor == null)
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

        FPSInventorySystem inventory = interactor.GetComponent<FPSInventorySystem>();
        TrySnapHeldHeadToConnectionPoint(rig, connectionPoint, interactor, inventory);
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

        if (headPickup.Assembly != null && headPickup.Assembly.Rig != null)
        {
            return headPickup.Assembly.Rig;
        }

        return headPickup.GetComponentInParent<FireHoseRig>();
    }

    private ActiveRigRecord FindRecordBySource(FireTruckHosePickupPointLite sourcePoint)
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

    private static void TrySnapHeldHeadToConnectionPoint(
        FireHoseRig rig,
        FireHoseConnectionPoint connectionPoint,
        GameObject interactor,
        FPSInventorySystem inventory)
    {
        if (rig == null || connectionPoint == null || inventory == null)
        {
            return;
        }

        FireHoseAssembly assembly = rig.Assembly;
        if (assembly == null)
        {
            return;
        }

        assembly.TryAttachHeadToMount(
            connectionPoint.ConnectAnchor,
            interactor,
            inventory,
            nozzle: null,
            snapPosition: true,
            snapRotation: true);
    }
}
