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
    [SerializeField] private FireTruckHosePickupPointLite liteSourcePoint;

    [Header("Runtime")]
    [SerializeField] private RigState state = RigState.Stowed;
    [SerializeField] private FireTruckHosePickupPoint sourcePoint;
    [SerializeField] private FireHoseConnectionPoint connectedHydrant;

    public RigState State => state;
    public FireHoseHeadPickup HeadPickup => headPickup;
    public FireHose Shooter => shooter;
    public FireHoseAssembly Assembly => assembly;
    public FireTruckHosePickupPoint SourcePoint => sourcePoint;
    public FireApparatusPumpSystem LitePumpSystem => liteSourcePoint != null ? liteSourcePoint.PumpSystem : null;
    public FireHoseConnectionPoint ConnectedHydrant => LitePumpSystem != null ? LitePumpSystem.GetConnectedHydrant(liteSourcePoint) : connectedHydrant;
    public bool IsConnectedToHydrant => LitePumpSystem != null ? LitePumpSystem.IsPortConnectedToHydrant(liteSourcePoint) : connectedHydrant != null;
    public FireHose ConnectedNozzle => LitePumpSystem != null ? LitePumpSystem.GetConnectedNozzle(liteSourcePoint) : assembly != null ? assembly.CurrentAttachedNozzle : null;
    public bool IsConnectedToNozzle => ConnectedNozzle != null;

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

    public void PrepareForDeploy(
        FireTruckHosePickupPoint source,
        Vector3 headSpawnPosition,
        Vector3 startKnotPosition,
        Vector3 startKnotNormal,
        Vector3 tailEndPosition)
    {
        ResolveReferences();

        sourcePoint = source;
        connectedHydrant = null;
        state = RigState.DeployedDry;

        if (deployable != null)
        {
            deployable.ResetPathFromStartKnot(startKnotPosition, startKnotNormal);
        }

        assembly?.ConfigureTailVisual(tailEndPosition, startKnotNormal);

        if (headTransform != null)
        {
            headTransform.position = headSpawnPosition;
        }

        if (headPickup != null)
        {
            headPickup.gameObject.SetActive(true);
            assembly?.SnapHeadToLatestKnot();
        }

        if (liteSourcePoint != null)
        {
            liteSourcePoint.RegisterRuntimeRig(this);
        }

        ApplyStateVisuals();
        assembly?.SyncAttachedNozzleSupply();
    }

    public void PrepareForDeploy(
        FireTruckHosePickupPoint source,
        Vector3 headSpawnPosition,
        Vector3 startKnotPosition,
        Vector3 startKnotNormal)
    {
        PrepareForDeploy(source, headSpawnPosition, startKnotPosition, startKnotNormal, startKnotPosition);
    }

    public void ConfigureLiteSourcePoint(FireTruckHosePickupPointLite sourcePoint)
    {
        liteSourcePoint = sourcePoint;
        liteSourcePoint?.RegisterRuntimeRig(this);
    }

    public bool TryConnectHydrant(FireHoseConnectionPoint connectionPoint)
    {
        ResolveReferences();
        if (state != RigState.DeployedDry || connectionPoint == null)
        {
            return false;
        }

        if (liteSourcePoint != null)
        {
            if (!liteSourcePoint.TryConnectHydrant(connectionPoint, this))
            {
                return false;
            }

            if (shooter != null && !shooter.TryConnectToSupply(connectionPoint))
            {
                liteSourcePoint.DisconnectHydrant(connectionPoint, this);
                return false;
            }

            state = RigState.ConnectedWet;
            ApplyStateVisuals();
            assembly?.SyncAttachedNozzleSupply();
            return true;
        }

        if (!connectionPoint.TryRegisterConnection(this))
        {
            return false;
        }

        if (shooter != null && !shooter.TryConnectToSupply(connectionPoint))
        {
            connectionPoint.ClearConnection(this);
            return false;
        }

        connectedHydrant = connectionPoint;
        state = RigState.ConnectedWet;
        ApplyStateVisuals();
        assembly?.SyncAttachedNozzleSupply();
        return true;
    }

    public void SetStowed()
    {
        connectedHydrant = null;
        state = RigState.Stowed;
        ApplyStateVisuals();
        assembly?.SyncAttachedNozzleSupply();
    }

    public bool DisconnectHydrant(FireHoseConnectionPoint expectedConnectionPoint = null)
    {
        FireHoseConnectionPoint activeHydrant = ConnectedHydrant;
        if (expectedConnectionPoint != null && !ReferenceEquals(activeHydrant, expectedConnectionPoint))
        {
            return false;
        }

        if (activeHydrant == null)
        {
            return false;
        }

        if (liteSourcePoint != null)
        {
            if (shooter != null)
            {
                shooter.DisconnectFromSupply(activeHydrant);
            }

            if (!liteSourcePoint.DisconnectHydrant(expectedConnectionPoint, this))
            {
                return false;
            }

            if (state != RigState.Stowed)
            {
                state = RigState.DeployedDry;
            }

            ApplyStateVisuals();
            assembly?.SyncAttachedNozzleSupply();
            return true;
        }

        if (shooter != null)
        {
            shooter.DisconnectFromSupply(activeHydrant);
        }

        activeHydrant.ClearConnection(this);
        connectedHydrant = null;
        if (state != RigState.Stowed)
        {
            state = RigState.DeployedDry;
        }

        ApplyStateVisuals();
        assembly?.SyncAttachedNozzleSupply();
        return true;
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

        shooterMount ??= assembly != null ? assembly.HeadRotationAnchor : headPickup != null ? headPickup.RotationAnchor : headTransform;

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
