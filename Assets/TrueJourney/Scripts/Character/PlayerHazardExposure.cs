using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHazardExposure : MonoBehaviour
{
    [Header("Smoke")]
    [SerializeField] private float smokeRiseSpeed = 2.5f;
    [SerializeField] private float smokeFallSpeed = 1.75f;
    [SerializeField, Range(0f, 1f)] private float smokeEnterThreshold = 0.08f;
    [SerializeField, Range(0f, 1f)] private float smokeExitThreshold = 0.04f;

    [Header("Runtime")]
    [SerializeField, Range(0f, 1f)] private float smokeDensity01;
    [SerializeField] private bool isInSmoke;

    private float smokeSubmissionDensity;
    private float lastSmokeSubmissionTime = float.NegativeInfinity;

    private const float SmokeSubmissionGraceSeconds = 0.1f;

    public float SmokeDensity01 => smokeDensity01;
    public bool IsInSmoke => isInSmoke;

    private void OnValidate()
    {
        smokeRiseSpeed = Mathf.Max(0f, smokeRiseSpeed);
        smokeFallSpeed = Mathf.Max(0f, smokeFallSpeed);
        smokeEnterThreshold = Mathf.Clamp01(smokeEnterThreshold);
        smokeExitThreshold = Mathf.Clamp(smokeExitThreshold, 0f, smokeEnterThreshold);
        smokeDensity01 = Mathf.Clamp01(smokeDensity01);
    }

    private void Update()
    {
        UpdateSmokeExposure(Time.deltaTime);
    }

    public void ReportSmokeExposure(float density01)
    {
        if (!HasRecentSmokeSubmission())
        {
            smokeSubmissionDensity = 0f;
        }

        lastSmokeSubmissionTime = Time.time;
        smokeSubmissionDensity = Mathf.Max(smokeSubmissionDensity, Mathf.Clamp01(density01));
    }

    private void UpdateSmokeExposure(float deltaTime)
    {
        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        float targetSmoke = HasRecentSmokeSubmission()
            ? smokeSubmissionDensity
            : 0f;
        float speed = targetSmoke >= smokeDensity01 ? smokeRiseSpeed : smokeFallSpeed;
        smokeDensity01 = Mathf.MoveTowards(smokeDensity01, targetSmoke, speed * safeDeltaTime);

        if (isInSmoke)
        {
            isInSmoke = smokeDensity01 > smokeExitThreshold;
        }
        else
        {
            isInSmoke = smokeDensity01 >= smokeEnterThreshold;
        }
    }

    private bool HasRecentSmokeSubmission()
    {
        return Time.time - lastSmokeSubmissionTime <= SmokeSubmissionGraceSeconds;
    }
}
