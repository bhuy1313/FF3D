using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FireApparatusPumpSystem : MonoBehaviour
{
    [System.Serializable]
    private class PortRuntimeState
    {
        public FireTruckHosePickupPointLite pickupPoint;
        public FireHoseRig rig;
        public FireHoseConnectionPoint connectedHydrant;
        public FireHose connectedNozzle;
    }

    [Header("Ports")]
    [SerializeField] private List<FireTruckHosePickupPointLite> pickupPoints = new List<FireTruckHosePickupPointLite>();

    [Header("Runtime")]
    [SerializeField] private bool isConnectedToFireHydrant;
    [SerializeField] private bool isConnectedToFireHoseNozzle;
    [SerializeField] private int connectedHydrantCount;
    [SerializeField] private int connectedNozzleCount;
    [SerializeField] private List<PortRuntimeState> runtimeStates = new List<PortRuntimeState>();

    public IReadOnlyList<FireTruckHosePickupPointLite> PickupPoints => pickupPoints;
    public bool IsConnectedToFireHydrant => isConnectedToFireHydrant;
    public bool IsConnectedToFireHoseNozzle => isConnectedToFireHoseNozzle;
    public int ConnectedHydrantCount => connectedHydrantCount;
    public int ConnectedNozzleCount => connectedNozzleCount;

    private void Awake()
    {
        ResolvePorts();
        RefreshRuntimeSummary();
    }

    private void Reset()
    {
        ResolvePorts();
        RefreshRuntimeSummary();
    }

    private void OnValidate()
    {
        ResolvePorts();
        RefreshRuntimeSummary();
    }

    public FireHoseRig GetCurrentRig(FireTruckHosePickupPointLite pickupPoint)
    {
        return FindState(pickupPoint)?.rig;
    }

    public FireHoseConnectionPoint GetConnectedHydrant(FireTruckHosePickupPointLite pickupPoint)
    {
        return FindState(pickupPoint)?.connectedHydrant;
    }

    public FireHose GetConnectedNozzle(FireTruckHosePickupPointLite pickupPoint)
    {
        return FindState(pickupPoint)?.connectedNozzle;
    }

    public FireHoseConnectionPoint GetSupplyHydrant()
    {
        for (int i = 0; i < runtimeStates.Count; i++)
        {
            PortRuntimeState state = runtimeStates[i];
            if (state != null && state.connectedHydrant != null)
            {
                return state.connectedHydrant;
            }
        }

        return null;
    }

    public bool HasActiveRig(FireTruckHosePickupPointLite pickupPoint)
    {
        return GetCurrentRig(pickupPoint) != null;
    }

    public bool IsPortConnectedToHydrant(FireTruckHosePickupPointLite pickupPoint)
    {
        return GetConnectedHydrant(pickupPoint) != null;
    }

    public bool IsPortConnectedToNozzle(FireTruckHosePickupPointLite pickupPoint)
    {
        return GetConnectedNozzle(pickupPoint) != null;
    }

    public void RegisterRuntimeRig(FireTruckHosePickupPointLite pickupPoint, FireHoseRig rig)
    {
        if (pickupPoint == null)
        {
            return;
        }

        PortRuntimeState state = GetOrCreateState(pickupPoint);
        state.rig = rig;
        state.connectedHydrant = null;
        state.connectedNozzle = null;
        RefreshRuntimeSummary();
    }

    public void ClearRuntimeRig(FireTruckHosePickupPointLite pickupPoint, FireHoseRig rig)
    {
        PortRuntimeState state = FindState(pickupPoint);
        if (state == null || !ReferenceEquals(state.rig, rig))
        {
            return;
        }

        if (state.connectedHydrant != null && rig != null)
        {
            state.connectedHydrant.ClearConnection(rig);
        }

        if (state.connectedNozzle != null)
        {
            state.connectedNozzle.DisconnectFromSupply(state.connectedHydrant);
        }

        state.connectedHydrant = null;
        state.connectedNozzle = null;
        state.rig = null;
        RefreshRuntimeSummary();
    }

    public bool TryConnectHydrant(FireTruckHosePickupPointLite pickupPoint, FireHoseConnectionPoint connectionPoint, FireHoseRig rig)
    {
        PortRuntimeState state = FindState(pickupPoint);
        if (state == null || connectionPoint == null || rig == null || !ReferenceEquals(state.rig, rig))
        {
            return false;
        }

        if (ReferenceEquals(state.connectedHydrant, connectionPoint))
        {
            SyncConnectedNozzleSupply(pickupPoint);
            return true;
        }

        if (!connectionPoint.TryRegisterConnection(rig))
        {
            return false;
        }

        state.connectedHydrant?.ClearConnection(rig);
        state.connectedHydrant = connectionPoint;
        SyncConnectedNozzleSupply(pickupPoint);
        RefreshRuntimeSummary();
        return true;
    }

    public bool DisconnectHydrant(FireTruckHosePickupPointLite pickupPoint, FireHoseConnectionPoint expectedConnectionPoint, FireHoseRig rig)
    {
        PortRuntimeState state = FindState(pickupPoint);
        if (state == null || rig == null || !ReferenceEquals(state.rig, rig))
        {
            return false;
        }

        if (expectedConnectionPoint != null && !ReferenceEquals(state.connectedHydrant, expectedConnectionPoint))
        {
            return false;
        }

        if (state.connectedHydrant == null)
        {
            return false;
        }

        FireHoseConnectionPoint previousConnectionPoint = state.connectedHydrant;
        state.connectedHydrant = null;
        previousConnectionPoint.ClearConnection(rig);
        SyncConnectedNozzleSupply(pickupPoint);
        RefreshRuntimeSummary();
        return true;
    }

    public void SetConnectedNozzle(FireTruckHosePickupPointLite pickupPoint, FireHose nozzle)
    {
        PortRuntimeState state = FindState(pickupPoint);
        if (state == null)
        {
            return;
        }

        if (ReferenceEquals(state.connectedNozzle, nozzle))
        {
            SyncConnectedNozzleSupply(pickupPoint);
            RefreshRuntimeSummary();
            return;
        }

        if (state.connectedNozzle != null)
        {
            state.connectedNozzle.DisconnectFromSupply(state.connectedHydrant);
        }

        state.connectedNozzle = nozzle;
        SyncConnectedNozzleSupply(pickupPoint);
        RefreshRuntimeSummary();
    }

    public void ClearConnectedNozzle(FireTruckHosePickupPointLite pickupPoint, FireHose nozzle)
    {
        PortRuntimeState state = FindState(pickupPoint);
        if (state == null || state.connectedNozzle == null)
        {
            return;
        }

        if (nozzle != null && !ReferenceEquals(state.connectedNozzle, nozzle))
        {
            return;
        }

        state.connectedNozzle.DisconnectFromSupply(state.connectedHydrant);
        state.connectedNozzle = null;
        RefreshRuntimeSummary();
    }

    public void SyncConnectedNozzleSupply(FireTruckHosePickupPointLite pickupPoint)
    {
        PortRuntimeState state = FindState(pickupPoint);
        if (state == null || state.connectedNozzle == null)
        {
            return;
        }

        FireHoseConnectionPoint supplyHydrant = GetSupplyHydrant();
        if (supplyHydrant != null)
        {
            state.connectedNozzle.TryConnectToSupply(supplyHydrant);
            return;
        }

        state.connectedNozzle.DisconnectFromSupply();
    }

    private void ResolvePorts()
    {
        for (int i = pickupPoints.Count - 1; i >= 0; i--)
        {
            if (pickupPoints[i] == null)
            {
                pickupPoints.RemoveAt(i);
            }
        }

        FireTruckHosePickupPointLite[] discoveredPickupPoints = GetComponentsInChildren<FireTruckHosePickupPointLite>(true);
        for (int i = 0; i < discoveredPickupPoints.Length; i++)
        {
            FireTruckHosePickupPointLite pickupPoint = discoveredPickupPoints[i];
            if (pickupPoint != null && !pickupPoints.Contains(pickupPoint))
            {
                pickupPoints.Add(pickupPoint);
            }
        }

        for (int i = runtimeStates.Count - 1; i >= 0; i--)
        {
            PortRuntimeState state = runtimeStates[i];
            if (state == null || state.pickupPoint == null)
            {
                runtimeStates.RemoveAt(i);
            }
        }

        RefreshRuntimeSummary();
    }

    private PortRuntimeState FindState(FireTruckHosePickupPointLite pickupPoint)
    {
        if (pickupPoint == null)
        {
            return null;
        }

        for (int i = 0; i < runtimeStates.Count; i++)
        {
            PortRuntimeState state = runtimeStates[i];
            if (state != null && ReferenceEquals(state.pickupPoint, pickupPoint))
            {
                return state;
            }
        }

        return null;
    }

    private PortRuntimeState GetOrCreateState(FireTruckHosePickupPointLite pickupPoint)
    {
        PortRuntimeState state = FindState(pickupPoint);
        if (state != null)
        {
            return state;
        }

        state = new PortRuntimeState
        {
            pickupPoint = pickupPoint
        };
        runtimeStates.Add(state);
        return state;
    }

    private void RefreshRuntimeSummary()
    {
        connectedHydrantCount = 0;
        connectedNozzleCount = 0;

        for (int i = 0; i < runtimeStates.Count; i++)
        {
            PortRuntimeState state = runtimeStates[i];
            if (state != null && state.connectedHydrant != null)
            {
                connectedHydrantCount++;
            }
            if (state != null && state.connectedNozzle != null)
            {
                connectedNozzleCount++;
            }
        }

        isConnectedToFireHydrant = connectedHydrantCount > 0;
        isConnectedToFireHoseNozzle = connectedNozzleCount > 0;
    }
}
