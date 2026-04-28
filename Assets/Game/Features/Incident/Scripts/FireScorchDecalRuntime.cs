using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(250)]
public sealed class FireScorchDecalRuntime : MonoBehaviour
{
    [SerializeField] private bool autoBindFireComponents = false;
    [SerializeField] private bool autoBindFireSimulationManagers = false;
    [SerializeField] private Material scorchMaterial;
    [SerializeField] private LayerMask surfaceMask = ~0;
    [SerializeField] [Min(0.2f)] private float scanInterval = 1f;
    [SerializeField] [Min(1)] private int maxClusterDecals = 96;

    private readonly List<FireSimulationScorchDecalBinder> simulationBinders = new List<FireSimulationScorchDecalBinder>();
    private float nextScanTime;

    private void OnEnable()
    {
        ScanScene();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextScanTime)
        {
            ScanScene();
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < simulationBinders.Count; i++)
        {
            if (simulationBinders[i] != null)
            {
                simulationBinders[i].enabled = false;
            }
        }
    }

    private void ScanScene()
    {
        nextScanTime = Time.unscaledTime + scanInterval;

        if (autoBindFireComponents)
        {
            BindFireComponents();
        }

        if (autoBindFireSimulationManagers)
        {
            BindSimulationManagers();
        }
    }

    private void BindFireComponents()
    {
        Fire[] fires = FindObjectsByType<Fire>(FindObjectsInactive.Exclude);
        for (int i = 0; i < fires.Length; i++)
        {
            Fire fire = fires[i];
            if (fire == null || fire.GetComponent<FireScorchDecalController>() != null)
            {
                continue;
            }

            FireScorchDecalController controller = fire.gameObject.AddComponent<FireScorchDecalController>();
            controller.Configure(scorchMaterial, surfaceMask);
        }
    }

    private void BindSimulationManagers()
    {
        FireSimulationManager[] managers = FindObjectsByType<FireSimulationManager>(FindObjectsInactive.Exclude);
        for (int i = 0; i < managers.Length; i++)
        {
            FireSimulationManager manager = managers[i];
            if (manager == null || manager.GetComponent<FireSimulationScorchDecalBinder>() != null)
            {
                continue;
            }

            FireSimulationScorchDecalBinder binder = manager.gameObject.AddComponent<FireSimulationScorchDecalBinder>();
            binder.Configure(manager, scorchMaterial, surfaceMask, maxClusterDecals);
            simulationBinders.Add(binder);
        }
    }
}
