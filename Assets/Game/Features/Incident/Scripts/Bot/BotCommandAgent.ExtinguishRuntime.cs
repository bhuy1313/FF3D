using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    [Header("Extinguish Task Flow")]
    [SerializeField] private BotExtinguishSubtask currentExtinguishSubtask;
    [SerializeField] private string extinguishTaskDetail = "Awaiting extinguish assignment.";
    [SerializeField] private string lastExtinguishFailureReason;
    [SerializeField] private float extinguishSubtaskStartedAtTime;

    private void StopExtinguisher()
    {
        if (activeExtinguisher != null)
        {
            activeExtinguisher.ClearExternalAimDirection(gameObject);
            activeExtinguisher.SetExternalSprayState(false, gameObject);
        }
    }

    private bool TryApplyWaterToFireGroup(IBotExtinguisherItem tool, IFireGroupTarget fireGroup, Vector3 firePosition)
    {
        if (!CanApplyWaterToFireGroup(tool, fireGroup, firePosition))
        {
            return false;
        }

        float waterAmount = Mathf.Max(0f, tool.ApplyWaterPerSecond) * Time.deltaTime;
        if (waterAmount <= 0f)
        {
            return false;
        }

        fireGroup.ApplyWater(waterAmount, gameObject, tool.SuppressionAgent);
        return true;
    }

    private bool CanApplyWaterToFireGroup(IBotExtinguisherItem tool, IFireGroupTarget fireGroup, Vector3 firePosition)
    {
        if (tool == null || fireGroup == null || !fireGroup.HasActiveFires)
        {
            return false;
        }

        Vector3 aimOrigin = UsesPreciseAim(tool) ? GetPreciseAimOrigin() : transform.position;
        Vector3 toFire = hasCurrentExtinguishLaunchDirection
            ? currentExtinguishLaunchDirection
            : GetAimPoint(tool, firePosition) - aimOrigin;
        if (toFire.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        if (!UsesPreciseAim(tool))
        {
            toFire.y = 0f;
        }

        Vector3 forward = UsesPreciseAim(tool) ? GetPreciseAimForward() : transform.forward;
        if (forward.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        if (!UsesPreciseAim(tool))
        {
            forward.y = 0f;
        }

        float facingDot = Vector3.Dot(forward.normalized, toFire.normalized);
        if (facingDot < sprayFacingThreshold)
        {
            return false;
        }

        float horizontalDistance = GetHorizontalDistance(aimOrigin, firePosition);
        float verticalDistance = Mathf.Abs(firePosition.y - aimOrigin.y);
        if (horizontalDistance > tool.MaxSprayDistance + ExtinguisherRangeSlack ||
            verticalDistance > tool.MaxVerticalReach + ExtinguisherRangeSlack)
        {
            return false;
        }

        return HasLineOfSightToFireTarget(aimOrigin, firePosition, null);
    }

    private void ApplyWaterToFireTarget(IBotExtinguisherItem tool, IFireTarget fireTarget)
    {
        if (tool == null || fireTarget == null)
        {
            return;
        }

        float waterAmount = Mathf.Max(0f, tool.ApplyWaterPerSecond) * Time.deltaTime;
        if (waterAmount <= 0f)
        {
            return;
        }

        fireTarget.ApplySuppression(waterAmount, tool.SuppressionAgent, gameObject);
    }

    private bool IsAimSettled(IBotExtinguisherItem tool, Vector3 firePosition)
    {
        if (tool == null)
        {
            return false;
        }

        if (!UsesPreciseAim(tool))
        {
            Vector3 flatToFire = firePosition - transform.position;
            flatToFire.y = 0f;
            if (flatToFire.sqrMagnitude <= 0.001f)
            {
                return true;
            }

            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            return Vector3.Dot(flatForward.normalized, flatToFire.normalized) >= 0.7f;
        }

        Vector3 aimOrigin = UsesPreciseAim(tool) ? GetPreciseAimOrigin() : transform.position;
        Vector3 toFire = hasCurrentExtinguishLaunchDirection
            ? currentExtinguishLaunchDirection
            : GetAimPoint(tool, firePosition) - aimOrigin;
        if (toFire.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        Vector3 forward = UsesPreciseAim(tool) ? GetPreciseAimForward() : transform.forward;
        if (forward.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        float facingDot = Vector3.Dot(forward.normalized, toFire.normalized);
        return facingDot >= sprayFacingThreshold;
    }

    private float ScoreSuppressionTool(
        IBotExtinguisherItem tool,
        Vector3 orderPoint,
        Vector3 firePosition,
        Vector3 toolPosition,
        IFireTarget fireTarget,
        BotExtinguishEngagementMode engagementMode)
    {
        float requiredHorizontalDistance = GetRequiredHorizontalDistanceForAim(tool, firePosition);
        float desiredHorizontalDistance = Mathf.Max(tool.PreferredSprayDistance, requiredHorizontalDistance);
        float preferredDistance = tool.PreferredSprayDistance;

        if (!UsesPreciseAim(tool) && fireTarget != null)
        {
            desiredHorizontalDistance = GetDesiredExtinguisherCenterDistance(tool, fireTarget);
            preferredDistance = GetDesiredExtinguisherStandOffDistance(tool);
        }

        Vector3 attackPosition = engagementMode == BotExtinguishEngagementMode.DirectBestTool
            ? orderPoint
            : ResolveExtinguishPosition(orderPoint, firePosition, desiredHorizontalDistance);
        float travelToAttack = Vector3.Distance(toolPosition, attackPosition);
        float fitPenalty = Mathf.Abs((!UsesPreciseAim(tool) && fireTarget != null ? GetDesiredExtinguisherStandOffDistance(tool) : desiredHorizontalDistance) - preferredDistance) * 0.35f;
        float rangePenalty = !UsesPreciseAim(tool) && fireTarget != null
            ? 0f
            : desiredHorizontalDistance > tool.MaxSprayDistance
            ? (desiredHorizontalDistance - tool.MaxSprayDistance) * 4f
            : 0f;
        float verticalPenalty = GetVerticalAimPenalty(toolPosition, firePosition);
        float throughputBonus = Mathf.Max(0f, tool.ApplyWaterPerSecond) * 0.1f;
        float hosePriorityBonus = engagementMode == BotExtinguishEngagementMode.DirectBestTool && UsesPreciseAim(tool)
            ? 10000f
            : 0f;
        return travelToAttack + fitPenalty + rangePenalty + verticalPenalty - throughputBonus - hosePriorityBonus;
    }

    private static bool IsUnsafeSuppressionToolForFire(IBotExtinguisherItem tool, IFireTarget fireTarget)
    {
        return tool != null &&
            fireTarget != null &&
            fireTarget.EvaluateSuppressionOutcome(tool.SuppressionAgent) == FireSuppressionOutcome.UnsafeWorsens;
    }

    private bool CanToolReachFire(
        IBotExtinguisherItem tool,
        BotExtinguishCommandMode orderMode,
        BotExtinguishEngagementMode engagementMode,
        Vector3 orderPoint,
        Vector3 firePosition,
        IFireGroupTarget fireGroup,
        IFireTarget fireTarget)
    {
        if (tool == null)
        {
            return false;
        }

        bool usePrecisionRoute = engagementMode == BotExtinguishEngagementMode.PrecisionFireHose;
        if (usePrecisionRoute)
        {
            if (!UsesPreciseAim(tool) || fireGroup == null || !fireGroup.HasActiveFires)
            {
                return false;
            }
        }
        else if (fireTarget == null || !fireTarget.IsBurning)
        {
            return false;
        }

        if (!usePrecisionRoute)
        {
            if (orderMode == BotExtinguishCommandMode.PointFire)
            {
                if (fireTarget == null || !fireTarget.IsBurning)
                {
                    return false;
                }

                if (CanExtinguishFromCurrentPosition(tool, firePosition, fireTarget))
                {
                    return true;
                }

                return CanReachDestination(orderPoint) || TryResolvePointFireApproachPosition(orderPoint, out _);
            }

            if (CanExtinguishFromCurrentPosition(tool, firePosition, fireTarget))
            {
                return true;
            }

            if (engagementMode == BotExtinguishEngagementMode.DirectBestTool)
            {
                return CanReachDestination(orderPoint) || TryResolvePointFireApproachPosition(orderPoint, out _);
            }

            if (TryResolveReachableReferencePosition(orderPoint, out Vector3 reachableOrderPoint) &&
                CanExtinguishFromPosition(tool, reachableOrderPoint, firePosition, fireTarget))
            {
                return true;
            }

            float fireReferenceSampleDistance = Mathf.Max(tool.MaxSprayDistance + tool.MaxVerticalReach, 8f);
            if (TryResolveReachableReferencePosition(firePosition, fireReferenceSampleDistance, out Vector3 reachableFireReference) &&
                CanExtinguishFromPosition(tool, reachableFireReference, firePosition, fireTarget))
            {
                return true;
            }

            float desiredCenterDistance = GetDesiredExtinguisherCenterDistance(tool, fireTarget);
            if (!TryResolveExtinguisherStandPosition(orderPoint, firePosition, desiredCenterDistance, out Vector3 standPosition) &&
                !TryResolveExtinguisherStandPosition(transform.position, firePosition, desiredCenterDistance, out standPosition))
            {
                return false;
            }

            return CanExtinguishFromPosition(tool, standPosition, firePosition, fireTarget);
        }

        float verticalOffset = Mathf.Abs(firePosition.y - transform.position.y);
        if (verticalOffset > tool.MaxVerticalReach)
        {
            return false;
        }

        float requiredHorizontalDistance = GetRequiredHorizontalDistanceForAim(tool, firePosition);
        float desiredHorizontalDistance = Mathf.Max(tool.PreferredSprayDistance, requiredHorizontalDistance);
        return desiredHorizontalDistance <= tool.MaxSprayDistance;
    }

    private static bool DoesToolMatchExtinguishMode(
        IBotExtinguisherItem tool,
        BotExtinguishCommandMode orderMode,
        BotExtinguishEngagementMode engagementMode)
    {
        if (tool == null)
        {
            return false;
        }

        if (engagementMode == BotExtinguishEngagementMode.PrecisionFireHose)
        {
            return UsesPreciseAim(tool);
        }

        if (engagementMode == BotExtinguishEngagementMode.DirectBestTool)
        {
            return true;
        }

        switch (orderMode)
        {
            case BotExtinguishCommandMode.FireGroup:
                return true;
            case BotExtinguishCommandMode.PointFire:
                return !UsesPreciseAim(tool);
            default:
                return true;
        }
    }

    private bool TryResolveExtinguisherStandPosition(Vector3 originPoint, Vector3 firePosition, float preferredDistance, out Vector3 standPosition)
    {
        standPosition = default;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        Vector3 primaryOffset = originPoint - firePosition;
        primaryOffset.y = 0f;
        if (primaryOffset.sqrMagnitude < 0.01f)
        {
            primaryOffset = transform.position - firePosition;
            primaryOffset.y = 0f;
        }

        if (primaryOffset.sqrMagnitude < 0.01f)
        {
            primaryOffset = transform.forward;
            primaryOffset.y = 0f;
        }

        Vector3 primaryDirection = primaryOffset.sqrMagnitude > 0.001f ? primaryOffset.normalized : Vector3.forward;
        float standDistance = Mathf.Max(MinExtinguisherStandOffDistance, preferredDistance);
        float sampleDistance = Mathf.Max(navMeshSampleDistance, standDistance + 2f, 8f);
        float bestScore = float.PositiveInfinity;
        NavMeshPath path = new NavMeshPath();
        float[] distanceScales = { 0.65f, 0.85f, 1f, 1.15f, 1.35f, 1.6f, 1.8f, 2.1f, 2.5f };

        for (int distanceIndex = 0; distanceIndex < distanceScales.Length; distanceIndex++)
        {
            float candidateDistance = Mathf.Max(MinExtinguisherStandOffDistance, standDistance * distanceScales[distanceIndex]);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * primaryDirection;
                Vector3 candidate = firePosition + direction * candidateDistance;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit navMeshHit, sampleDistance, navMeshAgent.areaMask))
                {
                    continue;
                }

                if (!NavMesh.CalculatePath(transform.position, navMeshHit.position, navMeshAgent.areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float horizontalDistance = GetHorizontalDistance(navMeshHit.position, firePosition);
                float fitPenalty = Mathf.Abs(horizontalDistance - standDistance);
                float directionPenalty = (1f - Mathf.Clamp01(Vector3.Dot((navMeshHit.position - firePosition).normalized, primaryDirection))) * 0.75f;
                float pathPenalty = GetNavMeshPathLength(path, transform.position) * 0.03f;
                float score = fitPenalty + directionPenalty + pathPenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    standPosition = navMeshHit.position;
                }
            }
        }

        return bestScore < float.PositiveInfinity;
    }

    private bool CanExtinguishFromCurrentPosition(IBotExtinguisherItem tool, Vector3 firePosition, IFireTarget fireTarget)
    {
        return CanExtinguishFromPosition(tool, transform.position, firePosition, fireTarget);
    }

    private bool CanExtinguishFromPosition(IBotExtinguisherItem tool, Vector3 position, Vector3 firePosition, IFireTarget fireTarget)
    {
        if (tool == null || fireTarget == null || !fireTarget.IsBurning)
        {
            return false;
        }

        if (GetDistanceToFireEdge(position, firePosition, fireTarget) > GetAllowedExtinguisherEdgeRange(tool))
        {
            return false;
        }

        return HasLineOfSightToFireTarget(position, firePosition, fireTarget);
    }

    private float GetDistanceToFireEdge(Vector3 fromPosition, Vector3 firePosition, IFireTarget fireTarget)
    {
        float fireRadius = GetTrackedExtinguisherFireRadius(fireTarget);
        return Mathf.Max(0f, Vector3.Distance(fromPosition, firePosition) - fireRadius);
    }

    private bool HasLineOfSightToFireTarget(Vector3 originPosition, Vector3 firePosition, IFireTarget fireTarget)
    {
        if (extinguishPlanState.EngagementMode == BotExtinguishEngagementMode.PrecisionFireHose &&
            IsExtinguisherTargetLocked(fireTarget))
        {
            return true;
        }

        Vector3 targetPoint = ResolveExtinguishVisibilityTargetPoint(firePosition, fireTarget);
        Vector3 origin = ResolveExtinguishVisibilityOrigin(originPosition, targetPoint);
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        Vector3 direction = toTarget / distance;
        float castStartOffset = Mathf.Min(0.05f, distance * 0.25f);
        Vector3 castOrigin = origin + direction * castStartOffset;
        float castDistance = Mathf.Max(0f, distance - castStartOffset);
        if (castDistance <= 0.001f)
        {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(castOrigin, direction, castDistance, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit nearestHit = default;
        bool hasNearestHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Collider collider = hit.collider;
            if (collider == null || ShouldIgnoreExtinguishOccluder(collider))
            {
                continue;
            }

            if (!hasNearestHit || hit.distance < nearestHit.distance)
            {
                nearestHit = hit;
                hasNearestHit = true;
            }
        }

        if (!hasNearestHit)
        {
            return true;
        }

        return IsColliderPartOfFireTarget(nearestHit.collider, fireTarget);
    }

    private Vector3 ResolveExtinguishVisibilityOrigin(Vector3 originPosition, Vector3 targetPoint)
    {
        if (ApproximatelySameXZ(originPosition, transform.position) && viewPoint != null)
        {
            return viewPoint.position;
        }

        Vector3 offsetOrigin = originPosition + Vector3.up * Mathf.Max(0.6f, headAimVerticalOffset);
        Vector3 toTarget = targetPoint - offsetOrigin;
        if (toTarget.sqrMagnitude <= 0.001f)
        {
            return offsetOrigin;
        }

        return offsetOrigin + toTarget.normalized * 0.05f;
    }

    private static Vector3 ResolveExtinguishVisibilityTargetPoint(Vector3 firePosition, IFireTarget fireTarget)
    {
        float fireRadius = fireTarget != null ? Mathf.Max(0f, fireTarget.GetWorldRadius()) : 0f;
        return firePosition + Vector3.up * Mathf.Min(0.5f, fireRadius * 0.35f);
    }

    private bool ShouldIgnoreExtinguishOccluder(Collider collider)
    {
        return collider != null &&
               (collider.transform.IsChildOf(transform) ||
                (activeExtinguisher is Component extinguisherComponent &&
                 collider.transform.IsChildOf(extinguisherComponent.transform)));
    }

    private static bool IsColliderPartOfFireTarget(Collider collider, IFireTarget fireTarget)
    {
        if (collider == null || fireTarget == null)
        {
            return false;
        }

        if (!(fireTarget is Component targetComponent) || targetComponent == null)
        {
            return false;
        }

        return collider.transform.IsChildOf(targetComponent.transform) ||
               targetComponent.transform.IsChildOf(collider.transform);
    }

    private static bool ApproximatelySameXZ(Vector3 a, Vector3 b)
    {
        Vector2 delta = new Vector2(a.x - b.x, a.z - b.z);
        return delta.sqrMagnitude <= 0.0001f;
    }

    private bool TryResolveReachableReferencePosition(Vector3 referencePoint, out Vector3 resolvedPosition)
    {
        return TryResolveReachableReferencePosition(referencePoint, Mathf.Max(navMeshSampleDistance, 2f), out resolvedPosition);
    }

    private bool TryResolveReachableReferencePosition(Vector3 referencePoint, float sampleDistance, out Vector3 resolvedPosition)
    {
        resolvedPosition = default;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        float effectiveSampleDistance = Mathf.Max(sampleDistance, navMeshSampleDistance, 2f);
        if (!NavMesh.SamplePosition(referencePoint, out NavMeshHit navMeshHit, effectiveSampleDistance, navMeshAgent.areaMask))
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, navMeshHit.position, navMeshAgent.areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        resolvedPosition = navMeshHit.position;
        return true;
    }

    private bool TryResolvePointFireApproachPosition(Vector3 scanOrigin, out Vector3 resolvedPosition)
    {
        resolvedPosition = default;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        float searchRadius = Mathf.Max(pointFireApproachSearchRadius, fireSearchRadius, navMeshSampleDistance, 2f);
        float ringStep = Mathf.Max(0.5f, pointFireApproachSampleStep);
        int directionCount = Mathf.Clamp(pointFireApproachDirections, 6, 24);
        float sampleDistance = Mathf.Max(navMeshSampleDistance, ringStep, 1f);
        float bestScore = float.PositiveInfinity;
        NavMeshPath path = new NavMeshPath();

        for (float radius = 0f; radius <= searchRadius + 0.01f; radius += ringStep)
        {
            int samples = radius <= 0.01f ? 1 : directionCount;
            for (int i = 0; i < samples; i++)
            {
                float angle = samples == 1 ? 0f : (360f * i) / samples;
                Vector3 offset = samples == 1
                    ? Vector3.zero
                    : Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                Vector3 candidate = scanOrigin + offset;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit navMeshHit, sampleDistance, navMeshAgent.areaMask))
                {
                    continue;
                }

                if (!NavMesh.CalculatePath(transform.position, navMeshHit.position, navMeshAgent.areaMask, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float toClickPenalty = GetHorizontalDistance(navMeshHit.position, scanOrigin);
                float heightPenalty = Mathf.Abs(navMeshHit.position.y - scanOrigin.y) * Mathf.Max(0f, pointFireApproachHeightWeight);
                float pathPenalty = GetNavMeshPathLength(path, transform.position) * 0.03f;
                float score = toClickPenalty + heightPenalty + pathPenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    resolvedPosition = navMeshHit.position;
                }
            }
        }

        return bestScore < float.PositiveInfinity;
    }

    private bool CanReachDestination(Vector3 destination)
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        return NavMesh.CalculatePath(transform.position, destination, navMeshAgent.areaMask, path) &&
               path.status == NavMeshPathStatus.PathComplete;
    }

    private bool TryResolvePreciseStandPosition(Vector3 requestedPoint, Vector3 firePosition, float preferredDistance, out Vector3 standPosition)
    {
        standPosition = default;
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        Vector3 preferredOffset = requestedPoint - firePosition;
        preferredOffset.y = 0f;
        if (preferredOffset.sqrMagnitude < 0.01f)
        {
            preferredOffset = transform.position - firePosition;
            preferredOffset.y = 0f;
        }

        if (preferredOffset.sqrMagnitude < 0.01f)
        {
            preferredOffset = transform.forward;
            preferredOffset.y = 0f;
        }

        Vector3 preferredDirection = preferredOffset.sqrMagnitude > 0.001f ? preferredOffset.normalized : Vector3.forward;
        float standDistance = Mathf.Max(0.5f, preferredDistance);
        float sampleDistance = Mathf.Max(navMeshSampleDistance, standDistance + 2f);
        float bestScore = float.PositiveInfinity;
        NavMeshPath path = new NavMeshPath();
        float[] distanceScales = { 0.85f, 1f, 1.15f };

        for (int distanceIndex = 0; distanceIndex < distanceScales.Length; distanceIndex++)
        {
            float candidateDistance = Mathf.Max(0.5f, standDistance * distanceScales[distanceIndex]);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * 22.5f;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * preferredDirection;
                Vector3 candidate = firePosition + direction * candidateDistance;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit navMeshHit, sampleDistance, navMeshAgent.areaMask))
                {
                    continue;
                }

                if (!NavMesh.CalculatePath(transform.position, navMeshHit.position, navMeshAgent.areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                Vector3 fromFire = navMeshHit.position - firePosition;
                fromFire.y = 0f;
                if (fromFire.sqrMagnitude <= 0.001f)
                {
                    continue;
                }

                float horizontalDistance = fromFire.magnitude;
                float fitPenalty = Mathf.Abs(horizontalDistance - standDistance);
                float directionPenalty = (1f - Mathf.Clamp01(Vector3.Dot(fromFire.normalized, preferredDirection))) * 2.5f;
                float pathPenalty = GetNavMeshPathLength(path, transform.position) * 0.04f;
                float orderPointPenalty = GetHorizontalDistance(navMeshHit.position, requestedPoint) * 0.08f;
                float score = fitPenalty + directionPenalty + pathPenalty + orderPointPenalty;

                if (score < bestScore)
                {
                    bestScore = score;
                    standPosition = navMeshHit.position;
                }
            }
        }

        return bestScore < float.PositiveInfinity;
    }

    private static float GetNavMeshPathLength(NavMeshPath path, Vector3 startPosition)
    {
        if (path == null || path.corners == null || path.corners.Length == 0)
        {
            return 0f;
        }

        float total = Vector3.Distance(startPosition, path.corners[0]);
        for (int i = 1; i < path.corners.Length; i++)
        {
            total += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }

        return total;
    }

    private IFireTarget FindClosestActiveFire(Vector3 orderPoint)
    {
        IFireTarget representativeTarget = FindClosestRepresentativeFireFromGroups(orderPoint, transform.position, fireSearchRadius);
        if (representativeTarget != null && representativeTarget.IsBurning)
        {
            return representativeTarget;
        }
        return FindClosestFallbackFireTarget(orderPoint, transform.position, fireSearchRadius, preferWithinRadius: true);
    }

    private IFireTarget ResolveActiveFireTarget(Vector3 orderPoint)
    {
        IFireTarget lockedTarget = GetLockedExtinguisherFireTarget();
        if (lockedTarget != null)
        {
            SetCurrentFireTarget(lockedTarget);
            return currentFireTarget;
        }

        if (currentFireTarget != null && currentFireTarget.IsBurning)
        {
            perceptionMemory?.RememberFire(currentFireTarget);
            BotRuntimeRegistry.SharedIncidentBlackboard.RememberFire(currentFireTarget);
            return currentFireTarget;
        }

        IFireTarget representativeTarget = FindClosestRepresentativeFireFromGroups(orderPoint, transform.position, fireSearchRadius);
        if (representativeTarget != null && representativeTarget.IsBurning)
        {
            SetCurrentFireTarget(representativeTarget);
        }
        else
        {
            SetCurrentFireTarget(FindClosestActiveFire(orderPoint));
        }

        if (currentFireTarget == null &&
            perceptionMemory != null &&
            perceptionMemory.TryGetNearestRecentFireGroup(orderPoint, fireSearchRadius, out IFireGroupTarget rememberedGroup))
        {
            SetCurrentFireTarget(ResolveRepresentativeFireTarget(rememberedGroup, transform.position));
        }

        if (currentFireTarget == null &&
            perceptionMemory != null &&
            perceptionMemory.TryGetNearestRecentFire(orderPoint, fireSearchRadius, out IFireTarget rememberedFire))
        {
            SetCurrentFireTarget(rememberedFire);
        }

        if (currentFireTarget == null &&
            BotRuntimeRegistry.SharedIncidentBlackboard.TryGetNearestRecentFireGroup(orderPoint, fireSearchRadius, out IFireGroupTarget sharedGroup))
        {
            SetCurrentFireTarget(ResolveRepresentativeFireTarget(sharedGroup, transform.position));
        }

        if (currentFireTarget == null &&
            BotRuntimeRegistry.SharedIncidentBlackboard.TryGetNearestRecentFire(orderPoint, fireSearchRadius, out IFireTarget sharedFire))
        {
            SetCurrentFireTarget(sharedFire);
        }

        return currentFireTarget;
    }

    private IFireTarget ResolvePointFireTarget(Vector3 scanOrigin)
    {
        IFireTarget lockedTarget = GetLockedExtinguisherFireTarget();
        if (lockedTarget != null)
        {
            SetCurrentFireTarget(lockedTarget);
            return currentFireTarget;
        }

        float keepRange = GetExtinguisherOrderAreaRadius();
        if (currentFireTarget != null && currentFireTarget.IsBurning)
        {
            float currentDistance = GetHorizontalDistance(transform.position, currentFireTarget.GetWorldPosition());
            if (currentDistance <= keepRange)
            {
                perceptionMemory?.RememberFire(currentFireTarget);
                BotRuntimeRegistry.SharedIncidentBlackboard.RememberFire(currentFireTarget);
                return currentFireTarget;
            }
        }

        if (interactionSensor != null &&
            interactionSensor.TryFindNearbyFireGroup(keepRange, out _, out IFireTarget nearbyFire))
        {
            SetCurrentFireTarget(nearbyFire);
            return currentFireTarget;
        }

        if (interactionSensor != null &&
            interactionSensor.TryFindFireGroupNearPoint(scanOrigin, fireSearchRadius, out _, out IFireTarget scanOriginFire))
        {
            SetCurrentFireTarget(scanOriginFire);
            return currentFireTarget;
        }

        if (perceptionMemory != null &&
            perceptionMemory.TryGetNearestRecentFireGroup(scanOrigin, fireSearchRadius, out IFireGroupTarget rememberedGroup))
        {
            SetCurrentFireTarget(ResolveRepresentativeFireTarget(rememberedGroup, transform.position));
            if (currentFireTarget != null)
            {
                return currentFireTarget;
            }
        }

        if (perceptionMemory != null &&
            perceptionMemory.TryGetNearestRecentFire(scanOrigin, fireSearchRadius, out IFireTarget rememberedFire))
        {
            SetCurrentFireTarget(rememberedFire);
            return currentFireTarget;
        }

        if (BotRuntimeRegistry.SharedIncidentBlackboard.TryGetNearestRecentFireGroup(scanOrigin, fireSearchRadius, out IFireGroupTarget sharedGroup))
        {
            SetCurrentFireTarget(ResolveRepresentativeFireTarget(sharedGroup, transform.position));
            if (currentFireTarget != null)
            {
                return currentFireTarget;
            }
        }

        if (BotRuntimeRegistry.SharedIncidentBlackboard.TryGetNearestRecentFire(scanOrigin, fireSearchRadius, out IFireTarget sharedFire))
        {
            SetCurrentFireTarget(sharedFire);
            return currentFireTarget;
        }

        SetCurrentFireTarget(null);
        return currentFireTarget;
    }

    private IFireTarget ResolveExtinguisherRouteTarget(Vector3 orderPoint)
    {
        IFireTarget lockedTarget = GetLockedExtinguisherFireTarget();
        if (lockedTarget != null)
        {
            SetCurrentFireTarget(lockedTarget);
            return currentFireTarget;
        }

        if (currentFireTarget != null && currentFireTarget.IsBurning)
        {
            float keepTargetRange = GetExtinguisherOrderAreaRadius();
            float currentTargetDistance = GetHorizontalDistance(transform.position, currentFireTarget.GetWorldPosition());
            if (sprayReadyTime >= 0f ||
                currentExtinguishSubtask == BotExtinguishSubtask.AimAtFire ||
                currentExtinguishSubtask == BotExtinguishSubtask.Spray ||
                currentTargetDistance <= keepTargetRange + ExtinguisherTargetStickinessRadiusSlack)
            {
                return currentFireTarget;
            }
        }

        if (IsNearOrderPoint(orderPoint))
        {
            IFireTarget representativeTarget = FindClosestRepresentativeFireFromGroups(
                orderPoint,
                transform.position,
                GetExtinguisherOrderAreaRadius());
            if (representativeTarget != null && representativeTarget.IsBurning)
            {
                SetCurrentFireTarget(representativeTarget);
                return currentFireTarget;
            }

            SetCurrentFireTarget(FindClosestActiveFireAroundOrderPoint(orderPoint, transform.position, GetExtinguisherOrderAreaRadius()));
            return currentFireTarget;
        }

        IFireTarget corridorRepresentativeTarget = FindClosestRepresentativeFireFromGroups(orderPoint, transform.position, fireSearchRadius);
        if (corridorRepresentativeTarget != null && corridorRepresentativeTarget.IsBurning)
        {
            Vector3 representativePosition = corridorRepresentativeTarget.GetWorldPosition();
            float representativeToOrderSq = (representativePosition - orderPoint).sqrMagnitude;
            float representativeToBotSq = (representativePosition - transform.position).sqrMagnitude;
            float representativeProgress;
            float representativeOffsetSq = GetDistanceToSegmentXZSquared(transform.position, orderPoint, representativePosition, out representativeProgress);
            if ((representativeProgress >= 0f && representativeProgress <= 1.05f &&
                 representativeOffsetSq <= extinguisherRouteCorridorWidth * extinguisherRouteCorridorWidth) ||
                representativeToOrderSq < fireSearchRadius * fireSearchRadius)
            {
                SetCurrentFireTarget(corridorRepresentativeTarget);
                return currentFireTarget;
            }

            if (representativeToBotSq < fireSearchRadius * fireSearchRadius)
            {
                SetCurrentFireTarget(corridorRepresentativeTarget);
                return currentFireTarget;
            }
        }

        SetCurrentFireTarget(FindClosestFallbackFireTarget(orderPoint, transform.position, fireSearchRadius, preferWithinRadius: true));
        return currentFireTarget;
    }

    private IFireTarget FindClosestActiveFireAroundOrderPoint(Vector3 orderPoint, Vector3 fromPosition, float searchRadius)
    {
        IFireTarget representativeTarget = FindClosestRepresentativeFireFromGroups(orderPoint, fromPosition, searchRadius);
        if (representativeTarget != null && representativeTarget.IsBurning)
        {
            float distanceToOrderSq = (representativeTarget.GetWorldPosition() - orderPoint).sqrMagnitude;
            if (distanceToOrderSq <= searchRadius * searchRadius)
            {
                return representativeTarget;
            }
        }

        return FindClosestFallbackFireTarget(orderPoint, fromPosition, searchRadius, preferWithinRadius: true);
    }

    private IFireTarget ResolveStickyFireGroupRepresentative(IFireTarget currentTarget, IFireGroupTarget fireGroup, Vector3 fromPosition)
    {
        if (currentTarget != null &&
            currentTarget.IsBurning &&
            fireGroup != null &&
            fireGroup.HasActiveFires &&
            IsFireWithinRouteDetectionRadius(
                currentTarget,
                fromPosition,
                Mathf.Max(routeFireDetectionRadius, GetExtinguisherOrderAreaRadius()) + ExtinguisherTargetStickinessRadiusSlack))
        {
            return currentTarget;
        }

        return ResolveRepresentativeFireTarget(fireGroup, fromPosition);
    }

    private IFireTarget FindClosestRepresentativeFireFromGroups(Vector3 orderPoint, Vector3 fromPosition, float searchRadius)
    {
        IFireTarget bestTarget = null;
        float bestDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = Mathf.Max(0.05f, searchRadius) * Mathf.Max(0.05f, searchRadius);

        foreach (IFireGroupTarget candidateGroup in BotRuntimeRegistry.ActiveFireGroups)
        {
            if (candidateGroup == null || !candidateGroup.HasActiveFires)
            {
                continue;
            }

            Vector3 candidatePosition = candidateGroup.GetClosestActiveFirePosition(fromPosition);
            float distanceToOrderSq = (candidatePosition - orderPoint).sqrMagnitude;
            if (distanceToOrderSq > searchRadiusSq)
            {
                continue;
            }

            IFireTarget representativeTarget = ResolveRepresentativeFireTarget(candidateGroup, fromPosition);
            if (representativeTarget == null || !representativeTarget.IsBurning)
            {
                continue;
            }

            if (BotRuntimeRegistry.Reservations.IsReservedByOther(representativeTarget, gameObject))
            {
                continue;
            }

            float distanceToBotSq = (representativeTarget.GetWorldPosition() - fromPosition).sqrMagnitude;
            if (distanceToBotSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceToBotSq;
            bestTarget = representativeTarget;
        }

        return bestTarget;
    }

    private IFireTarget FindClosestFallbackFireTarget(Vector3 orderPoint, Vector3 fromPosition, float searchRadius, bool preferWithinRadius)
    {
        IFireTarget bestFire = null;
        IFireTarget fallbackFire = null;
        float bestDistanceSq = float.PositiveInfinity;
        float fallbackDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = Mathf.Max(0.05f, searchRadius) * Mathf.Max(0.05f, searchRadius);

        foreach (IFireTarget candidate in BotRuntimeRegistry.ActiveFireTargets)
        {
            if (candidate == null || !candidate.IsBurning)
            {
                continue;
            }

            if (BotRuntimeRegistry.Reservations.IsReservedByOther(candidate, gameObject))
            {
                continue;
            }

            Vector3 firePosition = candidate.GetWorldPosition();
            float toFromSq = (firePosition - fromPosition).sqrMagnitude;
            float toOrderSq = (firePosition - orderPoint).sqrMagnitude;

            if ((!preferWithinRadius || toOrderSq <= searchRadiusSq) && toFromSq < bestDistanceSq)
            {
                bestDistanceSq = toFromSq;
                bestFire = candidate;
            }

            if (toFromSq < fallbackDistanceSq)
            {
                fallbackDistanceSq = toFromSq;
                fallbackFire = candidate;
            }
        }

        return bestFire != null ? bestFire : fallbackFire;
    }

    private static float GetDistanceToSegmentXZSquared(Vector3 start, Vector3 end, Vector3 point, out float progress)
    {
        Vector2 start2 = new Vector2(start.x, start.z);
        Vector2 end2 = new Vector2(end.x, end.z);
        Vector2 point2 = new Vector2(point.x, point.z);
        Vector2 segment = end2 - start2;
        float segmentLengthSq = segment.sqrMagnitude;
        if (segmentLengthSq <= 0.0001f)
        {
            progress = 0f;
            return (point2 - start2).sqrMagnitude;
        }

        progress = Vector2.Dot(point2 - start2, segment) / segmentLengthSq;
        Vector2 projected = start2 + segment * Mathf.Clamp01(progress);
        return (point2 - projected).sqrMagnitude;
    }

    private bool IsNearOrderPoint(Vector3 orderPoint)
    {
        float threshold = GetExtinguisherOrderAreaRadius();
        return (orderPoint - transform.position).sqrMagnitude <= threshold * threshold;
    }

    private float GetExtinguisherOrderAreaRadius()
    {
        float toolRadius = activeExtinguisher != null ? activeExtinguisher.MaxSprayDistance + 1.5f : 0f;
        return Mathf.Max(fireSearchRadius, toolRadius);
    }

    private void CompleteExtinguishOrder(string detail)
    {
        lastExtinguishFailureReason = string.Empty;
        SetExtinguishSubtask(BotExtinguishSubtask.Complete, detail);
        CompleteCurrentTask(detail);
        UpdateExtinguishDebugStage(ExtinguishDebugStage.Completed, detail);
        ClearExtinguishRuntimeState();
        behaviorContext?.ClearExtinguishOrder();
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }
    }

    private void FailActiveExtinguishOrder(string detail, BotTaskStatus failureStatus = BotTaskStatus.Failed)
    {
        SetExtinguishFailureReason(detail);
        FailCurrentTask(detail, failureStatus);
        UpdateExtinguishDebugStage(ExtinguishDebugStage.NoReachableTool, detail);
        ClearExtinguishRuntimeState();
        behaviorContext.ClearExtinguishOrder();
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = false;
        }
    }

    internal void SetExtinguishSubtask(BotExtinguishSubtask subtask, string detail)
    {
        if (currentExtinguishSubtask != subtask)
        {
            extinguishSubtaskStartedAtTime = Application.isPlaying ? Time.time : 0f;
        }

        currentExtinguishSubtask = subtask;
        extinguishTaskDetail = string.IsNullOrWhiteSpace(detail) ? "Executing extinguish order." : detail;
    }

    internal void SetExtinguishFailureReason(string detail)
    {
        lastExtinguishFailureReason = string.IsNullOrWhiteSpace(detail) ? string.Empty : detail;
    }

    internal string GetActiveExtinguishTaskDetail()
    {
        if (!string.IsNullOrWhiteSpace(lastExtinguishFailureReason))
        {
            return lastExtinguishFailureReason;
        }

        return string.IsNullOrWhiteSpace(extinguishTaskDetail)
            ? "Executing extinguish order."
            : extinguishTaskDetail;
    }

    private void ClearExtinguishRuntimeState()
    {
        ClearHeadAimFocus();
        ClearHandAimFocus();
        ResetExtinguishCrouchState();
        StopExtinguisher();
        StopExtinguishV2Tool();
        StopInterruptV2Tool();
        ClearExtinguisherTargetLock();
        SetPickupWindow(false);
        ReleaseCommittedTool();
        activeExtinguisher = null;
        preferredExtinguishTool = null;
        commandedPointFireTarget = null;
        commandedFireGroupTarget = null;
        SetCurrentFireGroupTarget(null);
        SetCurrentFireTarget(null);
        currentExtinguishTargetPosition = default;
        currentExtinguishAimPoint = default;
        currentExtinguishLaunchDirection = default;
        hasCurrentExtinguishTargetPosition = false;
        hasCurrentExtinguishAimPoint = false;
        hasCurrentExtinguishLaunchDirection = false;
        currentExtinguishTrajectoryPointCount = 0;
        extinguishStartupPending = false;
        sprayReadyTime = -1f;
        currentExtinguishSubtask = BotExtinguishSubtask.None;
        extinguishTaskDetail = "Awaiting extinguish assignment.";
        lastExtinguishFailureReason = string.Empty;
        extinguishSubtaskStartedAtTime = 0f;
        temporarilyRejectedExtinguishTool = null;
        temporarilyRejectedExtinguishToolUntilTime = 0f;
        extinguishV2State.Reset();
        interruptV2State.Reset();
        extinguishV2PauseSnapshot.Reset();
        isExtinguishV2Paused = false;
        activityDebug?.ResetExtinguish();
        activityDebug?.ResetInterrupt();
    }
}
