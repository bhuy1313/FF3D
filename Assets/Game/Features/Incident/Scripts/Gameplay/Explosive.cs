using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Explosive : MonoBehaviour, IInteractable, IEventListener
{
    [Header("Activation")]
    [SerializeField] private float explodeDelay = 2f;
    [SerializeField] private bool activateOnInteract = true;
    [SerializeField] private bool activateOnEvent = true;

    [Header("Explosion Effect")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private Transform effectSpawnPoint;

    [Header("Fire Activation")]
    [SerializeField] private float detectionRadius = 5f;
    [SerializeField] private FireSimulationManager fireSimulationManager;
    [SerializeField] private float ignitionDraftHeatAmount = 0.75f;

    [Header("After Explosion")]
    [SerializeField] private bool disableSourceObjectOnExplosion = true;
    [SerializeField] private bool destroySourceObjectOnExplosion = false;

    private Coroutine explodeRoutine;
    private bool isActivated;
    private bool hasExploded;

    private void Awake()
    {
        ResolveFireSimulationManager();
    }

    private void Reset()
    {
    }

    private void OnValidate()
    {
        detectionRadius = Mathf.Max(0f, detectionRadius);
        ignitionDraftHeatAmount = Mathf.Max(0f, ignitionDraftHeatAmount);
        ResolveFireSimulationManager();
    }

    public void Interact(GameObject interactor)
    {
        if (!activateOnInteract)
        {
            return;
        }

        Activate();
    }

    public void OnEventTriggered(GameObject eventSource, GameObject instigator)
    {
        if (!activateOnEvent)
        {
            return;
        }

        Activate();
    }

    [ContextMenu("Activate")]
    public void Activate()
    {
        if (isActivated || hasExploded)
        {
            return;
        }

        isActivated = true;
        explodeRoutine = StartCoroutine(ExplodeAfterDelay());
    }

    [ContextMenu("Explode Now")]
    public void ExplodeNow()
    {
        if (hasExploded)
        {
            return;
        }

        if (explodeRoutine != null)
        {
            StopCoroutine(explodeRoutine);
            explodeRoutine = null;
        }

        isActivated = true;
        Explode();
    }

    private IEnumerator ExplodeAfterDelay()
    {
        if (explodeDelay > 0f)
        {
            yield return new WaitForSeconds(explodeDelay);
        }

        explodeRoutine = null;
        Explode();
    }

    private void Explode()
    {
        if (hasExploded)
        {
            return;
        }

        hasExploded = true;

        SpawnExplosionEffect();
        ActivateNearbyFirePoints();

        if (destroySourceObjectOnExplosion)
        {
            Destroy(gameObject);
            return;
        }

        if (disableSourceObjectOnExplosion)
        {
            gameObject.SetActive(false);
        }
    }

    private void SpawnExplosionEffect()
    {
        if (explosionEffectPrefab == null)
        {
            return;
        }

        Transform spawnRoot = effectSpawnPoint != null ? effectSpawnPoint : transform;
        GameObject effectInstance = Instantiate(
            explosionEffectPrefab,
            spawnRoot.position,
            spawnRoot.rotation);

        if (!effectInstance.TryGetComponent(out OneShotParticleEffect oneShotEffect))
        {
            oneShotEffect = effectInstance.AddComponent<OneShotParticleEffect>();
        }

        oneShotEffect.Play();
    }

    private void ActivateNearbyFirePoints()
    {
        Vector3 center = effectSpawnPoint != null ? effectSpawnPoint.position : transform.position;
        FireSimulationManager simulationManager = ResolveFireSimulationManager();
        if (simulationManager != null && simulationManager.IsInitialized)
        {
            Bounds ignitionBounds = new Bounds(Vector3.zero, Vector3.one * detectionRadius * 2f);
            ignitionBounds.center = center;
            simulationManager.ApplyDraftHeatInBounds(ignitionBounds, ignitionDraftHeatAmount);
        }
        else
        {
            Debug.LogWarning(
                $"{nameof(Explosive)} on '{name}' could not resolve an initialized {nameof(FireSimulationManager)}. Legacy Fire fallback has been removed.",
                this);
        }
    }

    private FireSimulationManager ResolveFireSimulationManager()
    {
        if (fireSimulationManager == null)
        {
            fireSimulationManager = GetComponentInParent<FireSimulationManager>(true);
        }

        if (fireSimulationManager == null)
        {
            Transform root = transform.root;
            if (root != null)
            {
                fireSimulationManager = root.GetComponentInChildren<FireSimulationManager>(true);
            }
        }

        return fireSimulationManager;
    }

    private void OnDrawGizmosSelected()
    {
        Transform gizmoRoot = effectSpawnPoint != null ? effectSpawnPoint : transform;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(gizmoRoot.position, detectionRadius);
    }
}
