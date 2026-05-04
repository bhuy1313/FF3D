using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshObstacle))]
public sealed class FireNavigationObstacleController : MonoBehaviour
{
    [SerializeField] private FireSimulationBotTarget fireTarget;
    [SerializeField] private NavMeshObstacle navMeshObstacle;
    [SerializeField] [Min(0.05f)] private float radiusPadding = 0.35f;
    [SerializeField] [Min(0.1f)] private float minimumRadius = 0.65f;
    [SerializeField] [Min(0.5f)] private float obstacleHeight = 2.2f;

    private bool hasBeenDiscovered;

    private void Awake()
    {
        EnsureReferences();
        ConfigureObstacleDefaults();
        RefreshObstacleState();
    }

    private void OnEnable()
    {
        RefreshObstacleState();
    }

    private void Update()
    {
        RefreshObstacleState();
    }

    private void OnDisable()
    {
        SetObstacleEnabled(false);
    }

    public void Configure(FireSimulationBotTarget target)
    {
        fireTarget = target;
        EnsureReferences();
        ConfigureObstacleDefaults();
        RefreshObstacleShape();
        RefreshObstacleState();
    }

    public bool ActivateIfBurning()
    {
        EnsureReferences();
        if (fireTarget == null || !fireTarget.IsBurning)
        {
            RefreshObstacleState();
            return false;
        }

        hasBeenDiscovered = true;
        RefreshObstacleShape();
        RefreshObstacleState();
        return navMeshObstacle != null && navMeshObstacle.enabled;
    }

    private void EnsureReferences()
    {
        if (fireTarget == null)
        {
            fireTarget = GetComponent<FireSimulationBotTarget>();
        }

        if (navMeshObstacle == null)
        {
            navMeshObstacle = GetComponent<NavMeshObstacle>();
        }
    }

    private void ConfigureObstacleDefaults()
    {
        if (navMeshObstacle == null)
        {
            return;
        }

        navMeshObstacle.shape = NavMeshObstacleShape.Capsule;
        navMeshObstacle.carving = true;
        navMeshObstacle.carveOnlyStationary = false;
        navMeshObstacle.carvingMoveThreshold = 0.1f;
        navMeshObstacle.carvingTimeToStationary = 0f;
        navMeshObstacle.center = Vector3.up * (obstacleHeight * 0.5f);
        navMeshObstacle.height = obstacleHeight;
    }

    private void RefreshObstacleShape()
    {
        if (navMeshObstacle == null || fireTarget == null)
        {
            return;
        }

        float radius = Mathf.Max(minimumRadius, fireTarget.GetWorldRadius() + radiusPadding);
        navMeshObstacle.radius = radius;
        navMeshObstacle.height = Mathf.Max(obstacleHeight, radius * 2f);
        navMeshObstacle.center = Vector3.up * (navMeshObstacle.height * 0.5f);
    }

    private void RefreshObstacleState()
    {
        bool shouldEnable = hasBeenDiscovered && fireTarget != null && fireTarget.IsBurning && gameObject.activeInHierarchy;
        SetObstacleEnabled(shouldEnable);
    }

    private void SetObstacleEnabled(bool enabled)
    {
        if (navMeshObstacle != null && navMeshObstacle.enabled != enabled)
        {
            navMeshObstacle.enabled = enabled;
        }
    }
}
