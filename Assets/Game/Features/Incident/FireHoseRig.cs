using UnityEngine;

[DisallowMultipleComponent]
public class FireHoseRig : MonoBehaviour
{
    public enum RigState
    {
        Stowed = 0,
        DeployedDry = 1,
        ConnectedWet = 2
    }

    [Header("References")]
    [SerializeField] private FireHoseAssembly assembly;
    [SerializeField] private FireHoseDeployable deployable;
    [SerializeField] private FireHoseDeployed staticHose;
    [SerializeField] private FireHoseHeadPickup headPickup;
    [SerializeField] private FireHose shooter;
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform shooterMount;

    [Header("Runtime")]
    [SerializeField] private RigState state = RigState.Stowed;
    [SerializeField] private FireTruckHosePickupPoint sourcePoint;
    [SerializeField] private FireHoseConnectionPoint connectedHydrant;

    public RigState State => state;
    public FireHoseHeadPickup HeadPickup => headPickup;
    public FireHose Shooter => shooter;
    public FireHoseAssembly Assembly => assembly;
    public FireTruckHosePickupPoint SourcePoint => sourcePoint;
    public FireHoseConnectionPoint ConnectedHydrant => connectedHydrant;

    private void Awake()
    {
        ResolveReferences();
        ApplyStateVisuals();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void LateUpdate()
    {
        AlignShooterToMount();
    }

    public void PrepareForDeploy(FireTruckHosePickupPoint source, Vector3 headSpawnPosition, Vector3 startKnotPosition, Vector3 startKnotNormal)
    {
        ResolveReferences();

        sourcePoint = source;
        connectedHydrant = null;
        state = RigState.DeployedDry;

        if (deployable != null)
        {
            deployable.ResetPathFromStartKnot(startKnotPosition, startKnotNormal);
        }

        if (headTransform != null)
        {
            headTransform.position = headSpawnPosition;
        }

        if (headPickup != null)
        {
            headPickup.gameObject.SetActive(true);

            Rigidbody rb = headPickup.Rigidbody;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = false;
                rb.detectCollisions = true;
            }
        }

        ApplyStateVisuals();
    }

    public bool TryConnectHydrant(FireHoseConnectionPoint connectionPoint)
    {
        ResolveReferences();
        if (state != RigState.DeployedDry || connectionPoint == null)
        {
            return false;
        }

        if (shooter != null && !shooter.TryConnectToSupply(connectionPoint))
        {
            return false;
        }

        connectedHydrant = connectionPoint;
        state = RigState.ConnectedWet;
        ApplyStateVisuals();
        return true;
    }

    public void SetStowed()
    {
        connectedHydrant = null;
        state = RigState.Stowed;
        ApplyStateVisuals();
    }

    private void ResolveReferences()
    {
        assembly ??= GetComponent<FireHoseAssembly>();
        deployable ??= GetComponentInChildren<FireHoseDeployable>(true);
        staticHose ??= GetComponentInChildren<FireHoseDeployed>(true);
        headPickup ??= GetComponentInChildren<FireHoseHeadPickup>(true);
        shooter ??= GetComponentInChildren<FireHose>(true);

        if (headTransform == null)
        {
            headTransform = assembly != null ? assembly.HeadTransform : headPickup != null ? headPickup.transform : null;
        }

        shooterMount ??= headTransform;

        if (assembly != null)
        {
            assembly.ConfigureRig(this);
        }
    }

    private void ApplyStateVisuals()
    {
        if (headPickup != null)
        {
            headPickup.gameObject.SetActive(state != RigState.Stowed);
        }

        if (staticHose != null)
        {
            staticHose.gameObject.SetActive(state != RigState.Stowed);
        }

        if (deployable != null)
        {
            deployable.enabled = state != RigState.Stowed;
        }

        if (shooter != null)
        {
            shooter.gameObject.SetActive(state == RigState.ConnectedWet);
        }

        AlignShooterToMount();
    }

    private void AlignShooterToMount()
    {
        if (shooter == null || shooterMount == null)
        {
            return;
        }

        shooter.transform.SetPositionAndRotation(shooterMount.position, shooterMount.rotation);
    }
}
