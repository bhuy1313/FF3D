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

    private struct SpawnedObiRod
    {
        public GameObject Rod;
        public Transform Start;
        public GameObject End;
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

        SpawnedObiRod spawnedObiRod = SpawnObiRod(sourcePoint, root.transform, rootWorldPosition, startKnotNormal);
        if (spawnedObiRod.Start == null || spawnedObiRod.End == null)
        {
            return null;
        }

        spawnedObiRod.Start.name = "FireHose_Knot";
        spawnedObiRod.Start.gameObject.layer = sourcePoint.HeadLayer;

        FireHoseDeployable deployable = hoseBody.GetComponentInChildren<FireHoseDeployable>(true);
        if (deployable != null)
        {
            deployable.head = spawnedObiRod.Start;
        }

        assembly.ConfigureRig(rig);
        EnsureConnectorHeadPickup(spawnedObiRod.End, assembly);
        rig.PrepareForDeploy(sourcePoint, spawnedObiRod.Start.position, rootWorldPosition, startKnotNormal);

        return rig;
    }

    private static SpawnedObiRod SpawnObiRod(
        FireTruckHosePickupPoint sourcePoint,
        Transform fallbackParent,
        Vector3 startKnotPosition,
        Vector3 startKnotNormal)
    {
        if (sourcePoint == null)
        {
            return default;
        }

        GameObject rodPrefab = sourcePoint.ObiRodPrefab;
        GameObject startPrefab = sourcePoint.ObiRodStartPrefab;
        GameObject endPrefab = sourcePoint.ObiRodEndPrefab;
        if (rodPrefab == null || startPrefab == null || endPrefab == null)
        {
            return default;
        }

        Transform parent = fallbackParent;
        ObiSolver solver = Object.FindAnyObjectByType<ObiSolver>();
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

        GameObject obiRodEnd = Object.Instantiate(endPrefab, rodPosition, rodRotation, parent);
        obiRodEnd.name = "FireHose_ConnectorHead";

        Vector3 startOffset = rodRotation * new Vector3(-1.37f, 0f, 0f);
        GameObject obiRodStart = Object.Instantiate(startPrefab, rodPosition + startOffset, rodRotation, parent);
        obiRodStart.name = startPrefab.name;

        if (obiRod.TryGetComponent(out ObiRodPickup existingPickup))
        {
            existingPickup.SetPickupRigidbody(obiRodEnd.GetComponent<Rigidbody>());
        }
        else
        {
            ObiRodPickup pickup = obiRod.AddComponent<ObiRodPickup>();
            pickup.SetPickupRigidbody(obiRodEnd.GetComponent<Rigidbody>());
        }
        
        ObiParticleAttachment[] attachments = obiRod.GetComponents<ObiParticleAttachment>();
        if (attachments.Length > 0)
        {
            attachments[0].target = obiRodEnd.transform;
        }

        if (attachments.Length > 1)
        {
            attachments[1].target = obiRodStart.transform;
        }

        return new SpawnedObiRod
        {
            Rod = obiRod,
            Start = obiRodStart.transform,
            End = obiRodEnd
        };
    }

    private static FireHoseHeadPickup EnsureConnectorHeadPickup(GameObject obiRodEnd, FireHoseAssembly assembly)
    {
        if (obiRodEnd == null)
        {
            return null;
        }

        FireHoseHeadPickup pickup = obiRodEnd.GetComponent<FireHoseHeadPickup>();
        if (pickup == null)
        {
            pickup = obiRodEnd.AddComponent<FireHoseHeadPickup>();
        }

        pickup.ConfigureAssembly(assembly);
        return pickup;
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

        if (headPickup.Assembly != null && headPickup.Assembly.Rig != null)
        {
            return headPickup.Assembly.Rig;
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
