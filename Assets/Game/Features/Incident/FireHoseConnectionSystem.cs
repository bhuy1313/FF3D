using System.Collections.Generic;
using UnityEngine;
using Obi;

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

        FireHoseRig rig = CreateRuntimeRig(sourcePoint, startKnotPosition, startKnotNormal);
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

    private FireHoseRig CreateRuntimeRig(FireTruckHosePickupPoint sourcePoint, Vector3 rootWorldPosition, Vector3 startKnotNormal)
    {
        if (sourcePoint.HoseBodyPrefab == null)
        {
            return null;
        }

        Transform parent = sourcePoint.ResolveRigParent();

        GameObject root = new GameObject("FireHoseRig_Runtime");
        root.transform.SetParent(parent, false);
        root.transform.position = rootWorldPosition;
        root.transform.rotation = Quaternion.identity;

        FireHoseAssembly assembly = root.AddComponent<FireHoseAssembly>();
        FireHoseRig rig = root.AddComponent<FireHoseRig>();

        GameObject hoseBody = Instantiate(sourcePoint.HoseBodyPrefab, root.transform);
        hoseBody.name = sourcePoint.HoseBodyPrefab.name;

        Transform obiRodStart = SpawnObiRod(sourcePoint, root.transform, rootWorldPosition, startKnotNormal);
        if (obiRodStart == null)
        {
            return null;
        }

        obiRodStart.name = "FireHose_Head";
        obiRodStart.gameObject.layer = sourcePoint.HeadLayer;
        if (obiRodStart.GetComponent<FireHoseHeadPickup>() == null)
        {
            obiRodStart.gameObject.AddComponent<FireHoseHeadPickup>();
        }

        FireHoseDeployable deployable = hoseBody.GetComponentInChildren<FireHoseDeployable>(true);
        if (deployable != null)
        {
            deployable.head = obiRodStart;
        }

        assembly.ConfigureRig(rig);
        rig.PrepareForDeploy(sourcePoint, obiRodStart.position, rootWorldPosition, startKnotNormal);

        return rig;
    }

    private static Transform SpawnObiRod(
        FireTruckHosePickupPoint sourcePoint,
        Transform fallbackParent,
        Vector3 startKnotPosition,
        Vector3 startKnotNormal)
    {
        if (sourcePoint == null)
        {
            return null;
        }

        GameObject rodPrefab = sourcePoint.ObiRodPrefab;
        GameObject startPrefab = sourcePoint.ObiRodStartPrefab;
        if (rodPrefab == null || startPrefab == null)
        {
            return null;
        }

        Transform parent = fallbackParent;
        ObiSolver solver = Object.FindFirstObjectByType<ObiSolver>();
        if (solver != null)
        {
            parent = solver.transform;
        }

        Vector3 normal = startKnotNormal.sqrMagnitude > 0.0001f ? startKnotNormal.normalized : Vector3.up;
        Vector3 rodAxis = Vector3.ProjectOnPlane(sourcePoint.transform.forward, normal);
        if (rodAxis.sqrMagnitude <= 0.0001f)
        {
            rodAxis = Vector3.ProjectOnPlane(sourcePoint.transform.right, normal);
        }

        if (rodAxis.sqrMagnitude <= 0.0001f)
        {
            rodAxis = Vector3.Cross(normal, Vector3.up);
        }

        if (rodAxis.sqrMagnitude <= 0.0001f)
        {
            rodAxis = Vector3.right;
        }

        rodAxis.Normalize();

        rodAxis = -rodAxis;

        Vector3 startPosition = startKnotPosition + normal * 0.08f;
        Vector3 rodPosition = startPosition + rodAxis * 1.37f;
        Quaternion rodRotation = Quaternion.LookRotation(Vector3.Cross(normal, rodAxis), normal);

        GameObject obiRod = Object.Instantiate(rodPrefab, rodPosition, rodRotation, parent);
        obiRod.name = rodPrefab.name;

        Vector3 startOffset = rodRotation * new Vector3(-1.37f, 0f, 0f);
        GameObject obiRodStart = Object.Instantiate(startPrefab, rodPosition + startOffset, rodRotation, parent);
        obiRodStart.name = startPrefab.name;
        
        ObiParticleAttachment[] attachments = obiRod.GetComponents<ObiParticleAttachment>();
        if (attachments.Length > 1)
        {
            attachments[1].target = obiRodStart.transform;
        }

        return obiRodStart.transform;
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
