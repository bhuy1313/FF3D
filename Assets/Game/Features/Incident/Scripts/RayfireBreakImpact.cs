using System.Collections;
using RayFire;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RayfireBreakImpact
{
    private const int FragmentForceRetryCount = 4;
    private const float ImpactRayBackstep = 0.25f;

    private static Runner runner;

    public enum DirectionMode
    {
        CameraRay,
        ImpactDirection
    }

    private struct GunImpactSettings
    {
        public RayfireGun.ImpactType Type;
        public float Strength;
        public float Radius;
        public float Offset;
    }

    public static void DemolishWithImpact(
        RayfireRigid rigid,
        GameObject source,
        Vector3 impactPoint,
        Vector3 impactDirection,
        bool applyFragmentForce,
        DirectionMode directionMode = DirectionMode.CameraRay)
    {
        if (rigid == null)
        {
            return;
        }

        RayfireGun sourceGun = ResolveGunFromSource(source);
        Ray impactRay = BuildImpactRay(source, impactPoint, impactDirection, directionMode);
        GunImpactSettings settings = CaptureSettings(sourceGun);

        rigid.Demolish();

        if (sourceGun != null)
        {
            sourceGun.Shoot(impactRay.origin, impactRay.direction);
        }

        if (applyFragmentForce && sourceGun != null)
        {
            ApplyGunForceToFragments(rigid, settings, impactRay, impactPoint);
            GetRunner().StartCoroutine(ApplyGunForceToFragmentsOverPhysics(rigid, settings, impactRay, impactPoint));
        }
    }

    public static Ray BuildImpactRay(
        GameObject source,
        Vector3 impactPoint,
        Vector3 impactDirection,
        DirectionMode directionMode = DirectionMode.CameraRay)
    {
        if (directionMode == DirectionMode.ImpactDirection)
        {
            Vector3 direction = impactDirection.sqrMagnitude > 0.001f
                ? impactDirection.normalized
                : ResolveSourceDirection(source, impactPoint);
            return new Ray(impactPoint - (direction * ImpactRayBackstep), direction);
        }

        Camera activeCamera = Camera.main;
        if (activeCamera != null)
        {
            return activeCamera.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        }

        if (source != null)
        {
            Transform cameraRoot = source.transform.Find("PlayerCameraRoot");
            if (cameraRoot != null)
            {
                return new Ray(cameraRoot.position, cameraRoot.forward);
            }

            Vector3 sourceDirection = ResolveSourceDirection(source, impactPoint);
            if (sourceDirection.sqrMagnitude > 0.001f)
            {
                return new Ray(source.transform.position, sourceDirection);
            }
        }

        Vector3 resolvedDirection = impactDirection.sqrMagnitude > 0.001f
            ? impactDirection.normalized
            : Vector3.forward;
        return new Ray(impactPoint - resolvedDirection, resolvedDirection);
    }

    private static Vector3 ResolveSourceDirection(GameObject source, Vector3 impactPoint)
    {
        if (source == null)
        {
            return Vector3.zero;
        }

        Vector3 sourceDirection = impactPoint - source.transform.position;
        return sourceDirection.sqrMagnitude > 0.001f
            ? sourceDirection.normalized
            : Vector3.zero;
    }

    private static RayfireGun ResolveGunFromSource(GameObject source)
    {
        if (source == null)
        {
            return null;
        }

        return source.GetComponentInChildren<RayfireGun>();
    }

    private static GunImpactSettings CaptureSettings(RayfireGun sourceGun)
    {
        if (sourceGun == null)
        {
            return default;
        }

        return new GunImpactSettings
        {
            Type = sourceGun.type,
            Strength = sourceGun.strength,
            Radius = sourceGun.radius,
            Offset = sourceGun.offset
        };
    }

    private static IEnumerator ApplyGunForceToFragmentsOverPhysics(
        RayfireRigid rigid,
        GunImpactSettings settings,
        Ray impactRay,
        Vector3 impactPoint)
    {
        for (int i = 0; i < FragmentForceRetryCount; i++)
        {
            yield return new WaitForFixedUpdate();
            ApplyGunForceToFragments(rigid, settings, impactRay, impactPoint);
        }
    }

    private static void ApplyGunForceToFragments(
        RayfireRigid rigid,
        GunImpactSettings settings,
        Ray impactRay,
        Vector3 impactPoint)
    {
        if (rigid == null || rigid.fragments == null || settings.Strength <= 0f)
        {
            return;
        }

        Vector3 direction = impactRay.direction.sqrMagnitude > 0.001f
            ? impactRay.direction.normalized
            : Vector3.forward;
        Vector3 forceOrigin = impactPoint + (settings.Offset * direction);

        for (int i = 0; i < rigid.fragments.Count; i++)
        {
            RayfireRigid fragment = rigid.fragments[i];
            if (fragment == null)
            {
                continue;
            }

            Rigidbody fragmentRigidbody = fragment.physics.rigidBody != null
                ? fragment.physics.rigidBody
                : fragment.GetComponent<Rigidbody>();
            if (fragmentRigidbody == null || fragmentRigidbody.isKinematic)
            {
                continue;
            }

            if (settings.Type == RayfireGun.ImpactType.AddExplosionForce)
            {
                fragmentRigidbody.AddExplosionForce(
                    settings.Strength,
                    forceOrigin,
                    settings.Radius,
                    0f,
                    ForceMode.VelocityChange);
            }
            else
            {
                fragmentRigidbody.AddForceAtPosition(
                    direction * settings.Strength,
                    impactPoint,
                    ForceMode.VelocityChange);
            }
        }
    }

    private static Runner GetRunner()
    {
        if (runner != null)
        {
            return runner;
        }

        GameObject runnerObject = new GameObject("RayfireBreakImpactRunner");
        Object.DontDestroyOnLoad(runnerObject);
        runner = runnerObject.AddComponent<Runner>();
        return runner;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        runner = null;
    }

    private sealed class Runner : MonoBehaviour
    {
        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (runner == this)
            {
                runner = null;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive)
            {
                return;
            }

            if (Object.FindAnyObjectByType<GameMaster>() != null)
            {
                return;
            }

            Destroy(gameObject);
        }
    }
}
