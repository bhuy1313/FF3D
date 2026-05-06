using Obi;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class FireHoseObiTailBridge : MonoBehaviour
{
    [SerializeField] private FireHoseDeployable source;
    [SerializeField] private Transform nozzle;
    [SerializeField] private ObiParticleAttachment knotAttachment;
    [SerializeField] private ObiParticleAttachment nozzleAttachment;
    [SerializeField] private float knotNormalOffset = 0.03f;
    [SerializeField] private bool drawDebug = true;

    private Transform knotAnchor;
    private Transform nozzleAnchor;

    void Awake()
    {
        EnsureAnchors();
        BindAttachments();
        UpdateAnchors();
    }

    void OnEnable()
    {
        EnsureAnchors();
        BindAttachments();
        UpdateAnchors();
    }

    void LateUpdate()
    {
        EnsureAnchors();
        BindAttachments();
        UpdateAnchors();
        DrawDebug();
    }

    void OnDisable()
    {
        if (knotAttachment != null && knotAttachment.target == knotAnchor)
        {
            knotAttachment.target = null;
        }

        if (nozzleAttachment != null && nozzleAttachment.target == nozzleAnchor)
        {
            nozzleAttachment.target = null;
        }
    }

    void OnDestroy()
    {
        if (Application.isPlaying)
        {
            DestroyAnchor(knotAnchor);
            DestroyAnchor(nozzleAnchor);
        }
    }

    void EnsureAnchors()
    {
        if (knotAnchor == null)
        {
            knotAnchor = CreateAnchor("Knot Anchor");
        }

        if (nozzleAnchor == null)
        {
            nozzleAnchor = CreateAnchor("Nozzle Anchor");
        }
    }

    Transform CreateAnchor(string anchorName)
    {
        var anchorObject = new GameObject(anchorName);
        anchorObject.hideFlags = HideFlags.HideAndDontSave;
        anchorObject.transform.SetParent(transform, false);
        return anchorObject.transform;
    }

    void DestroyAnchor(Transform anchor)
    {
        if (anchor == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(anchor.gameObject);
        }
        else
        {
            DestroyImmediate(anchor.gameObject);
        }
    }

    void BindAttachments()
    {
        if (knotAttachment != null && knotAttachment.target != knotAnchor)
        {
            knotAttachment.target = knotAnchor;
        }

        if (nozzleAttachment != null && nozzleAttachment.target != nozzleAnchor)
        {
            nozzleAttachment.target = nozzleAnchor;
        }
    }

    void UpdateAnchors()
    {
        if (nozzleAnchor != null)
        {
            if (nozzle != null)
            {
                nozzleAnchor.SetPositionAndRotation(nozzle.position, nozzle.rotation);
            }
            else
            {
                nozzleAnchor.SetPositionAndRotation(transform.position, transform.rotation);
            }
        }

        if (knotAnchor == null)
        {
            return;
        }

        if (TryGetLatestKnot(out Knot latestKnot))
        {
            Vector3 knotPosition = latestKnot.Position + latestKnot.Normal * knotNormalOffset;
            Quaternion knotRotation = Quaternion.LookRotation(GetAnchorForward(), latestKnot.Normal);
            knotAnchor.SetPositionAndRotation(knotPosition, knotRotation);
            return;
        }

        if (nozzleAnchor != null)
        {
            knotAnchor.SetPositionAndRotation(nozzleAnchor.position, nozzleAnchor.rotation);
        }
        else
        {
            knotAnchor.SetPositionAndRotation(transform.position, transform.rotation);
        }
    }

    bool TryGetLatestKnot(out Knot latestKnot)
    {
        latestKnot = default;

        if (source == null || source.Path == null || source.Path.Knots == null || source.Path.Knots.Count == 0)
        {
            return false;
        }

        latestKnot = source.Path.Knots[source.Path.Knots.Count - 1];
        return true;
    }

    Vector3 GetAnchorForward()
    {
        if (nozzle != null && nozzle.forward.sqrMagnitude > 0.0001f)
        {
            return nozzle.forward.normalized;
        }

        if (transform.forward.sqrMagnitude > 0.0001f)
        {
            return transform.forward.normalized;
        }

        return Vector3.forward;
    }

    void DrawDebug()
    {
        if (!drawDebug || knotAnchor == null || nozzleAnchor == null)
        {
            return;
        }

        Debug.DrawLine(knotAnchor.position, nozzleAnchor.position, Color.magenta);
        Debug.DrawRay(knotAnchor.position, knotAnchor.up * 0.25f, Color.red);
        Debug.DrawRay(nozzleAnchor.position, nozzleAnchor.forward * 0.25f, Color.cyan);
    }
}
