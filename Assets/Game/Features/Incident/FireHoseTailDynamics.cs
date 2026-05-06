using UnityEngine;

[DefaultExecutionOrder(500)]
public class FireHoseTailDynamics : MonoBehaviour
{
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private float slackDistance = 1.5f;
    [SerializeField] private float pullSmoothTime = 0.12f;
    [SerializeField] private float maxPullSpeed = 12f;
    [SerializeField] private float snapDistance = 5f;
    [SerializeField] private bool drawDebug = true;

    private Vector3 planarVelocity;

    void LateUpdate()
    {
        if (startPoint == null || endPoint == null)
        {
            return;
        }

        Vector3 startPlanar = new Vector3(startPoint.position.x, 0f, startPoint.position.z);
        Vector3 endPlanar = new Vector3(endPoint.position.x, 0f, endPoint.position.z);
        Vector3 toStart = startPlanar - endPlanar;
        float distance = toStart.magnitude;

        if (distance <= slackDistance)
        {
            planarVelocity = Vector3.zero;
            DrawDebug(distance, startPlanar, endPlanar);
            return;
        }

        Vector3 direction = toStart / Mathf.Max(distance, 0.0001f);
        Vector3 targetPlanar = startPlanar - direction * slackDistance;

        Vector3 nextPlanar;
        if (distance >= snapDistance)
        {
            nextPlanar = targetPlanar;
            planarVelocity = Vector3.zero;
        }
        else
        {
            nextPlanar = Vector3.SmoothDamp(
                endPlanar,
                targetPlanar,
                ref planarVelocity,
                Mathf.Max(0.01f, pullSmoothTime),
                Mathf.Max(0.01f, maxPullSpeed),
                Time.deltaTime);
        }

        endPoint.position = new Vector3(nextPlanar.x, endPoint.position.y, nextPlanar.z);
        DrawDebug(distance, startPlanar, nextPlanar);
    }

    void DrawDebug(float distance, Vector3 startPlanar, Vector3 endPlanar)
    {
        if (!drawDebug)
        {
            return;
        }

        Vector3 startWorld = new Vector3(startPlanar.x, startPoint.position.y, startPlanar.z);
        Vector3 endWorld = new Vector3(endPlanar.x, endPoint.position.y, endPlanar.z);

        Debug.DrawLine(startWorld, endWorld, distance > slackDistance ? Color.magenta : Color.gray);
        Debug.DrawRay(endWorld, Vector3.up * 0.25f, Color.red);
        Debug.DrawRay(startWorld, Vector3.up * 0.25f, Color.cyan);
    }
}
