using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Breakable : MonoBehaviour, IDamageable, IBotBreakableTarget
{
    public enum BreakableType
    {
        Wood,
        Stone
    }

    [Header("Type")]
    [SerializeField] private BreakableType breakableType = BreakableType.Wood;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private bool resetHealthOnEnable = true;
    [SerializeField] private bool invincible = false;

    [Header("Break")]
    [SerializeField] private bool destroyOnBreak = false;
    [SerializeField] private float destroyDelay = 0f;
    [SerializeField] private bool deactivateOnBreak = true;
    [SerializeField] private bool disableCollidersOnBreak = true;
    [SerializeField] private bool disableRenderersOnBreak = true;
    [SerializeField] private GameObject brokenPrefab;
    [SerializeField] private Transform brokenSpawnPoint;

    [Header("Events")]
    [SerializeField] private UnityEvent onDamaged;
    [SerializeField] private UnityEvent onBroken;

    private bool isBroken;

    public BreakableType Type => breakableType;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsBroken => isBroken;
    public bool CanBeClearedByBot => !isBroken && breakableType == BreakableType.Wood;

    private void OnEnable()
    {
        BotRuntimeRegistry.RegisterBreakableTarget(this);

        if (resetHealthOnEnable)
        {
            currentHealth = maxHealth;
        }

        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        isBroken = currentHealth <= 0f;
    }

    private void OnDisable()
    {
        BotRuntimeRegistry.UnregisterBreakableTarget(this);
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }

    public void TakeBreakDamage(float amount, GameObject source, Vector3 hitPoint, Vector3 hitNormal)
    {
        TakeDamage(amount, source, hitPoint, hitNormal);
    }

    public void TakeDamage(float amount, GameObject source, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (invincible || isBroken || amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        onDamaged?.Invoke();

        if (currentHealth <= 0f)
        {
            Break(source, hitPoint, hitNormal);
        }
    }

    private void Break(GameObject source, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isBroken)
        {
            return;
        }

        isBroken = true;

        if (brokenPrefab != null)
        {
            Transform spawn = brokenSpawnPoint != null ? brokenSpawnPoint : transform;
            Instantiate(brokenPrefab, spawn.position, spawn.rotation);
        }

        if (disableCollidersOnBreak)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        if (disableRenderersOnBreak)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        onBroken?.Invoke();

        if (destroyOnBreak)
        {
            Destroy(gameObject, Mathf.Max(0f, destroyDelay));
        }
        else if (deactivateOnBreak)
        {
            gameObject.SetActive(false);
        }
    }
}
