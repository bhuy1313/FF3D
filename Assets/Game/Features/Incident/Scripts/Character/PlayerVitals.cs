using System;
using UnityEngine;

public class PlayerVitals : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float healthRegenPerSecond = 0f;
    [SerializeField] private float healthRegenDelay = 3f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegenPerSecond = 15f;
    [SerializeField] private float staminaRegenDelay = 1f;

    [Header("Oxygen")]
    [SerializeField] private float maxOxygen = 100f;
    [SerializeField] private float oxygenRegenPerSecond = 0f;
    [SerializeField] private float oxygenRegenDelay = 0f;
    [SerializeField] private float oxygenDamagePerSecond = 5f;

    [Header("Runtime")]
    [SerializeField] private float currentHealth;
    [SerializeField] private float currentStamina;
    [SerializeField] private float currentOxygen;

    private float lastHealthChangeTime;
    private float lastStaminaUseTime;
    private float lastOxygenUseTime;
    private IPlayerExternalOxygenSource externalOxygenSource;

    public float CurrentHealth => currentHealth;
    public float CurrentStamina => currentStamina;
    public float CurrentOxygen => currentOxygen;
    public bool HasActiveExternalOxygenSupply => externalOxygenSource != null && externalOxygenSource.IsSupplyingOxygen;
    public float ExternalOxygenSupplyPercent01 => externalOxygenSource != null
        ? Mathf.Clamp01(externalOxygenSource.OxygenSupplyPercent01)
        : 0f;
    public float DisplayedOxygenPercent => HasActiveExternalOxygenSupply
        ? ExternalOxygenSupplyPercent01
        : OxygenPercent;

    public float HealthPercent => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
    public float StaminaPercent => maxStamina <= 0f ? 0f : currentStamina / maxStamina;
    public float OxygenPercent => maxOxygen <= 0f ? 0f : currentOxygen / maxOxygen;

    public bool IsAlive => currentHealth > 0f;

    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnStaminaChanged;
    public event Action<float, float> OnOxygenChanged;
    public event Action OnDeath;

    private void Awake()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentOxygen = maxOxygen;
        lastHealthChangeTime = Time.time;
        lastStaminaUseTime = Time.time;
        lastOxygenUseTime = Time.time;
    }

    private void Update()
    {
        RegenerateHealth();
        RegenerateStamina();
        RegenerateOxygen();
        ApplyOxygenDamage();
    }

    public bool TryUseStamina(float amount)
    {
        if (amount <= 0f || currentStamina < amount)
        {
            return false;
        }

        SetStamina(currentStamina - amount);
        lastStaminaUseTime = Time.time;
        return true;
    }

    public void RestoreStamina(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetStamina(currentStamina + amount);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || !IsAlive)
        {
            return;
        }

        SetHealth(currentHealth - amount);
        lastHealthChangeTime = Time.time;
        if (currentHealth <= 0f)
        {
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || !IsAlive)
        {
            return;
        }

        SetHealth(currentHealth + amount);
    }

    public void ConsumeOxygen(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        float remainingAmount = amount;
        if (externalOxygenSource != null && externalOxygenSource.IsSupplyingOxygen)
        {
            float suppliedAmount = Mathf.Clamp(externalOxygenSource.ConsumeSuppliedOxygen(amount), 0f, amount);
            remainingAmount = Mathf.Max(0f, amount - suppliedAmount);
        }

        if (remainingAmount <= 0f)
        {
            return;
        }

        SetOxygen(currentOxygen - remainingAmount);
        lastOxygenUseTime = Time.time;
    }

    public void RestoreOxygen(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetOxygen(currentOxygen + amount);
    }

    public void BindExternalOxygenSource(IPlayerExternalOxygenSource source)
    {
        if (source == null)
        {
            return;
        }

        externalOxygenSource = source;
    }

    public void UnbindExternalOxygenSource(IPlayerExternalOxygenSource source)
    {
        if (source != null && ReferenceEquals(externalOxygenSource, source))
        {
            externalOxygenSource = null;
        }
    }

    private void RegenerateHealth()
    {
        if (healthRegenPerSecond <= 0f || currentHealth <= 0f || currentHealth >= maxHealth)
        {
            return;
        }

        if (Time.time - lastHealthChangeTime < healthRegenDelay)
        {
            return;
        }

        SetHealth(currentHealth + healthRegenPerSecond * Time.deltaTime);
    }

    private void RegenerateStamina()
    {
        if (staminaRegenPerSecond <= 0f || currentStamina >= maxStamina)
        {
            return;
        }

        if (Time.time - lastStaminaUseTime < staminaRegenDelay)
        {
            return;
        }

        SetStamina(currentStamina + staminaRegenPerSecond * Time.deltaTime);
    }

    private void RegenerateOxygen()
    {
        if (oxygenRegenPerSecond <= 0f || currentOxygen >= maxOxygen)
        {
            return;
        }

        if (Time.time - lastOxygenUseTime < oxygenRegenDelay)
        {
            return;
        }

        SetOxygen(currentOxygen + oxygenRegenPerSecond * Time.deltaTime);
    }

    private void ApplyOxygenDamage()
    {
        if (currentOxygen > 0f || HasActiveExternalOxygenSupply || oxygenDamagePerSecond <= 0f || !IsAlive)
        {
            return;
        }

        TakeDamage(oxygenDamagePerSecond * Time.deltaTime);
    }

    private void SetHealth(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, maxHealth);
        if (Mathf.Approximately(currentHealth, clamped))
        {
            return;
        }

        currentHealth = clamped;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void SetStamina(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, maxStamina);
        if (Mathf.Approximately(currentStamina, clamped))
        {
            return;
        }

        currentStamina = clamped;
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    private void SetOxygen(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, maxOxygen);
        if (Mathf.Approximately(currentOxygen, clamped))
        {
            return;
        }

        currentOxygen = clamped;
        OnOxygenChanged?.Invoke(currentOxygen, maxOxygen);
    }
}