using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class IncidentAudioAutoBinder : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float rescanInterval = 0.75f;

    private float nextScanTime;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        QueueImmediateScan();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScanTime)
        {
            return;
        }

        ScanScene();
    }

    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        QueueImmediateScan();
    }

    private void QueueImmediateScan()
    {
        nextScanTime = 0f;
    }

    private void ScanScene()
    {
        nextScanTime = Time.unscaledTime + Mathf.Max(0.1f, rescanInterval);
        EnsureBindingsForFires();
        EnsureBindingsForFireGroups();
        EnsureBindingsForExtinguishers();
    }

    private static void EnsureBindingsForFires()
    {
        Fire[] fires = FindObjectsByType<Fire>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fires.Length; i++)
        {
            Fire fire = fires[i];
            if (fire != null && fire.GetComponent<FireAudioEmitter>() == null)
            {
                fire.gameObject.AddComponent<FireAudioEmitter>();
            }
        }
    }

    private static void EnsureBindingsForFireGroups()
    {
        FireGroup[] fireGroups = FindObjectsByType<FireGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < fireGroups.Length; i++)
        {
            FireGroup fireGroup = fireGroups[i];
            if (fireGroup != null && fireGroup.GetComponent<FireGroupAudioController>() == null)
            {
                fireGroup.gameObject.AddComponent<FireGroupAudioController>();
            }
        }
    }

    private static void EnsureBindingsForExtinguishers()
    {
        FireExtinguisher[] extinguishers = FindObjectsByType<FireExtinguisher>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        for (int i = 0; i < extinguishers.Length; i++)
        {
            FireExtinguisher extinguisher = extinguishers[i];
            if (extinguisher != null && extinguisher.GetComponent<FireExtinguisherAudio>() == null)
            {
                extinguisher.gameObject.AddComponent<FireExtinguisherAudio>();
            }
        }
    }
}
