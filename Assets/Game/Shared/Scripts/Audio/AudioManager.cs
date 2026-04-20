using UnityEngine;

[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    private const string HostObjectName = "AudioManager";

    private static AudioManager instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static GameObject EnsureHostObject()
    {
        if (instance == null)
        {
            instance = FindAnyObjectByType<AudioManager>(FindObjectsInactive.Include);
        }

        if (instance != null)
        {
            instance.gameObject.name = HostObjectName;
            return instance.gameObject;
        }

        GameObject existingHost = FindNamedHostObject();
        if (existingHost == null)
        {
            existingHost = new GameObject(HostObjectName);
        }

        instance = existingHost.GetComponent<AudioManager>();
        if (instance == null)
        {
            instance = existingHost.AddComponent<AudioManager>();
        }

        return instance.gameObject;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        gameObject.name = HostObjectName;
        DontDestroyOnLoad(gameObject);

        if (GetComponent<AudioService>() == null)
        {
            gameObject.AddComponent<AudioService>();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private static GameObject FindNamedHostObject()
    {
        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.parent == null && candidate.name == HostObjectName)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }
}
