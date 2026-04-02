using UnityEngine;

public partial class BotCommandAgent
{
    private void ResolveHandAimReference()
    {
        if (handAimTarget != null)
        {
            return;
        }

        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            if (childTransforms[i] != null && childTransforms[i].name == "Aimtarget")
            {
                handAimTarget = childTransforms[i];
                return;
            }
        }
    }

    private void UpdateHandAimTarget()
    {
        if (handAimTarget == null)
        {
            return;
        }

        Vector3 desiredPosition = handAimFocusActive
            ? handAimFocusWorldPosition
            : GetDefaultHandAimPosition();

        if (!handAimWorldPositionInitialized)
        {
            currentHandAimWorldPosition = desiredPosition;
            handAimWorldPositionInitialized = true;
        }
        else
        {
            float blend = handAimTargetLerpSpeed > 0f
                ? 1f - Mathf.Exp(-handAimTargetLerpSpeed * Time.deltaTime)
                : 1f;
            currentHandAimWorldPosition = Vector3.Lerp(currentHandAimWorldPosition, desiredPosition, blend);
        }

        handAimTarget.position = currentHandAimWorldPosition;
    }

    private void SetHandAimFocus(Vector3 worldPosition)
    {
        handAimFocusWorldPosition = worldPosition;
        handAimFocusActive = true;
    }

    private void ClearHandAimFocus()
    {
        handAimFocusActive = false;
    }

    private Vector3 GetDefaultHandAimPosition()
    {
        Vector3 origin = GetPreciseAimOrigin();
        return origin + transform.forward * Mathf.Max(0.5f, handAimDefaultDistance);
    }

    private Vector3 GetPreciseAimOrigin()
    {
        if (viewPoint != null)
        {
            return viewPoint.position;
        }

        return transform.position + Vector3.up * handAimVerticalOffset;
    }

    private Vector3 GetPreciseAimForward()
    {
        if (handAimTarget != null)
        {
            Vector3 origin = GetPreciseAimOrigin();
            Vector3 direction = handAimTarget.position - origin;
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }
        }

        return transform.forward.sqrMagnitude > 0.001f
            ? transform.forward.normalized
            : Vector3.forward;
    }
}
