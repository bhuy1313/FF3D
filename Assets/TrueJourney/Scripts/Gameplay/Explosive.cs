using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private int maxDetectedColliders = 32;
    [SerializeField] private LayerMask detectionMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("After Explosion")]
    [SerializeField] private bool disableSourceObjectOnExplosion = true;
    [SerializeField] private bool destroySourceObjectOnExplosion = false;

    private readonly HashSet<Fire> detectedFires = new HashSet<Fire>();
    private Collider[] detectionBuffer;
    private Coroutine explodeRoutine;
    private bool isActivated;
    private bool hasExploded;

    private void Awake()
    {
        EnsureDetectionBuffer();
    }

    private void Reset()
    {
        EnsureDetectionBuffer();
    }

    private void OnValidate()
    {
        maxDetectedColliders = Mathf.Max(1, maxDetectedColliders);
        detectionRadius = Mathf.Max(0f, detectionRadius);
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
        EnsureDetectionBuffer();

        Vector3 center = effectSpawnPoint != null ? effectSpawnPoint.position : transform.position;
        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            detectionRadius,
            detectionBuffer,
            detectionMask,
            triggerInteraction);

        detectedFires.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = detectionBuffer[i];
            if (hit == null)
            {
                continue;
            }

            Fire fire = hit.GetComponentInParent<Fire>();
            if (fire == null || !detectedFires.Add(fire))
            {
                continue;
            }

            if (!fire.AllowRegrowFromZero)
            {
                fire.SetAllowRegrowFromZero(true);
            }
        }
    }

    private void EnsureDetectionBuffer()
    {
        if (maxDetectedColliders < 1)
        {
            maxDetectedColliders = 1;
        }

        if (detectionBuffer == null || detectionBuffer.Length != maxDetectedColliders)
        {
            detectionBuffer = new Collider[maxDetectedColliders];
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform gizmoRoot = effectSpawnPoint != null ? effectSpawnPoint : transform;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(gizmoRoot.position, detectionRadius);
    }
}
