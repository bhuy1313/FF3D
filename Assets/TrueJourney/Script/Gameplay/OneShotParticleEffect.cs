using UnityEngine;

[DisallowMultipleComponent]
public class OneShotParticleEffect : MonoBehaviour
{
    [SerializeField] private bool playOnEnable = true;

    private ParticleSystem[] particleSystems;
    private bool isPlaying;

    private void Awake()
    {
        CacheParticleSystems();
        ConfigureAsOneShot();
    }

    private void OnEnable()
    {
        if (!playOnEnable)
        {
            return;
        }

        Play();
    }

    public void Play()
    {
        CacheParticleSystems();
        ConfigureAsOneShot();

        if (particleSystems == null || particleSystems.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
            {
                continue;
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }

        isPlaying = true;
    }

    private void Update()
    {
        if (!isPlaying || particleSystems == null || particleSystems.Length == 0)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps != null && ps.IsAlive(true))
            {
                return;
            }
        }

        isPlaying = false;
        Destroy(gameObject);
    }

    private void CacheParticleSystems()
    {
        if (particleSystems == null || particleSystems.Length == 0)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    private void ConfigureAsOneShot()
    {
        if (particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.None;
        }
    }
}
