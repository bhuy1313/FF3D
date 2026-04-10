using UnityEngine;
using UnityEngine.AI;
using TrueJourney.BotBehavior;

public partial class BotCommandAgent
{
    private void StopExtinguisher()
    {
        if (activeExtinguisher != null)
        {
            activeExtinguisher.ClearExternalAimDirection(gameObject);
            activeExtinguisher.SetExternalSprayState(false, gameObject);
        }
    }

    private void TryApplyWaterToFireGroup(IBotExtinguisherItem tool, IFireGroupTarget fireGroup, Vector3 firePosition)
    {
        if (tool == null || fireGroup == null)
        {
            return;
        }

        Vector3 aimOrigin = UsesPreciseAim(tool) ? GetPreciseAimOrigin() : transform.position;
        Vector3 toFire = hasCurrentExtinguishLaunchDirection
            ? currentExtinguishLaunchDirection
            : GetAimPoint(tool, firePosition) - aimOrigin;
        if (toFire.sqrMagnitude <= 0.001f)
        {
            return;
        }

        if (!UsesPreciseAim(tool))
        {
            toFire.y = 0f;
        }

        Vector3 forward = UsesPreciseAim(tool) ? GetPreciseAimForward() : transform.forward;
        if (forward.sqrMagnitude <= 0.001f)
        {
            return;
        }

        if (!UsesPreciseAim(tool))
        {
            forward.y = 0f;
        }

        float facingDot = Vector3.Dot(forward.normalized, toFire.normalized);
        if (facingDot < sprayFacingThreshold)
        {
            return;
        }

        float waterAmount = Mathf.Max(0f, tool.ApplyWaterPerSecond) * Time.deltaTime;
        if (waterAmount <= 0f)
        {
            return;
        }

        fireGroup.ApplyWater(waterAmount);
    }

    private void TryApplyWaterToFireTarget(IBotExtinguisherItem tool, IFireTarget fireTarget, Vector3 firePosition)
    {
        if (tool == null || fireTarget == null || !fireTarget.IsBurning)
        {
            return;
        }

        if (GetDistanceToFireEdge(transform.position, firePosition, fireTarget) > GetAllowedExtinguisherEdgeRange(tool))
        {
            return;
        }

        if (!HasLineOfSightToFireTarget(transform.position, firePosition, fireTarget))
        {
            return;
        }

        float waterAmount = Mathf.Max(0f, tool.ApplyWaterPerSecond) * Time.deltaTime;
        if (waterAmount <= 0f)
        {
            return;
        }

        fireTarget.ApplySuppression(waterAmount, tool.SuppressionAgent);
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

    private float ScoreSuppressionTool(IBotExtinguisherItem tool, Vector3 orderPoint, Vector3 firePosition, Vector3 toolPosition, IFireTarget fireTarget)
    {
        float requiredHorizontalDistance = GetRequiredHorizontalDistanceForAim(tool, firePosition);
        float desiredHorizontalDistance = Mathf.Max(tool.PreferredSprayDistance, requiredHorizontalDistance);
        float preferredDistance = tool.PreferredSprayDistance;

        if (!UsesPreciseAim(tool) && fireTarget != null)
        {
            desiredHorizontalDistance = GetDesiredExtinguisherCenterDistance(tool, fireTarget);
            preferredDistance = GetDesiredExtinguisherStandOffDistance(tool);
        }

        Vector3 attackPosition = ResolveExtinguishPosition(orderPoint, firePosition, desiredHorizontalDistance);
        float travelToAttack = Vector3.Distance(toolPosition, attackPosition);
        float fitPenalty = Mathf.Abs((!UsesPreciseAim(tool) && fireTarget != null ? GetDesiredExtinguisherStandOffDistance(tool) : desiredHorizontalDistance) - preferredDistance) * 0.35f;
        float rangePenalty = !UsesPreciseAim(tool) && fireTarget != null
            ? 0f
            : desiredHorizontalDistance > tool.MaxSprayDistance
            ? (desiredHorizontalDistance - tool.MaxSprayDistance) * 4f
            : 0f;
        float verticalPenalty = GetVerticalAimPenalty(toolPosition, firePosition);
        float throughputBonus = Mathf.Max(0f, tool.ApplyWaterPerSecond) * 0.1f;
        return travelToAttack + fitPenalty + rangePenalty + verticalPenalty - throughputBonus;
    }

    private bool CanToolReachFire(IBotExtinguisherItem tool, BotExtinguishCommandMode orderMode, Vector3 orderPoint, Vector3 firePosition, IFireGroupTarget fireGroup, IFireTarget fireTarget)
    {
        if (tool == null)
        {
            return false;
        }

        if (UsesPreciseAim(tool))
        {
            if (fireGroup == null || !fireGroup.HasActiveFires)
            {
                return false;
            }
        }
        else if (fireTarget == null || !fireTarget.IsBurning)
        {
            return false;
        }

        if (!UsesPreciseAim(tool))
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

    private static bool DoesToolMatchExtinguishMode(IBotExtinguisherItem tool, BotExtinguishCommandMode orderMode)
    {
        if (tool == null)
        {
            return false;
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
        if (IsExtinguisherTargetLocked(fireTarget))
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
        IFireTarget bestFire = null;
        IFireTarget nearestFire = null;
        float bestDistanceSq = float.PositiveInfinity;
        float nearestDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = fireSearchRadius * fireSearchRadius;

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

            float distanceSq = (candidate.GetWorldPosition() - orderPoint).sqrMagnitude;
            if (distanceSq < nearestDistanceSq)
            {
                nearestDistanceSq = distanceSq;
                nearestFire = candidate;
            }

            if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            bestFire = candidate;
        }

        return bestFire != null ? bestFire : nearestFire;
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

        SetCurrentFireTarget(FindClosestActiveFire(orderPoint));
        if (currentFireTarget == null && perceptionMemory != null && perceptionMemory.TryGetNearestRecentFire(orderPoint, fireSearchRadius, out IFireTarget rememberedFire))
        {
            SetCurrentFireTarget(rememberedFire);
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
            interactionSensor.TryFindNearbyFire(keepRange, out IFireTarget nearbyFire))
        {
            SetCurrentFireTarget(nearbyFire);
            return currentFireTarget;
        }

        if (interactionSensor != null &&
            interactionSensor.TryFindFireNearPoint(scanOrigin, fireSearchRadius, out IFireTarget scanOriginFire))
        {
            SetCurrentFireTarget(scanOriginFire);
            return currentFireTarget;
        }

        if (perceptionMemory != null &&
            perceptionMemory.TryGetNearestRecentFire(scanOrigin, fireSearchRadius, out IFireTarget rememberedFire))
        {
            SetCurrentFireTarget(rememberedFire);
            return currentFireTarget;
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
            if (sprayReadyTime >= 0f || currentTargetDistance <= keepTargetRange)
            {
                return currentFireTarget;
            }
        }

        if (IsNearOrderPoint(orderPoint))
        {
            SetCurrentFireTarget(FindClosestActiveFireAroundOrderPoint(orderPoint, transform.position, GetExtinguisherOrderAreaRadius()));
            return currentFireTarget;
        }

        IFireTarget corridorFire = null;
        IFireTarget bestFire = null;
        IFireTarget fallbackFire = null;
        float bestCorridorProgress = float.PositiveInfinity;
        float bestCorridorOffsetSq = float.PositiveInfinity;
        float bestDistanceSq = float.PositiveInfinity;
        float fallbackDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = fireSearchRadius * fireSearchRadius;
        Vector3 botPosition = transform.position;

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
            float toOrderSq = (firePosition - orderPoint).sqrMagnitude;
            float toBotSq = (firePosition - botPosition).sqrMagnitude;
            float progress;
            float lateralDistanceSq = GetDistanceToSegmentXZSquared(botPosition, orderPoint, firePosition, out progress);

            if (progress >= 0f && progress <= 1.05f && lateralDistanceSq <= extinguisherRouteCorridorWidth * extinguisherRouteCorridorWidth)
            {
                if (progress < bestCorridorProgress || (Mathf.Approximately(progress, bestCorridorProgress) && lateralDistanceSq < bestCorridorOffsetSq))
                {
                    bestCorridorProgress = progress;
                    bestCorridorOffsetSq = lateralDistanceSq;
                    corridorFire = candidate;
                }
            }

            if (toOrderSq < searchRadiusSq && toBotSq < bestDistanceSq)
            {
                bestDistanceSq = toBotSq;
                bestFire = candidate;
            }

            if (toBotSq < fallbackDistanceSq)
            {
                fallbackDistanceSq = toBotSq;
                fallbackFire = candidate;
            }
        }

        SetCurrentFireTarget(corridorFire != null
            ? corridorFire
            : bestFire != null
                ? bestFire
                : fallbackFire);
        return currentFireTarget;
    }

    private IFireTarget FindClosestActiveFireAroundOrderPoint(Vector3 orderPoint, Vector3 fromPosition, float searchRadius)
    {
        IFireTarget bestFire = null;
        IFireTarget fallbackFire = null;
        float bestDistanceSq = float.PositiveInfinity;
        float fallbackDistanceSq = float.PositiveInfinity;
        float searchRadiusSq = searchRadius * searchRadius;

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

            if (toOrderSq <= searchRadiusSq && toFromSq < bestDistanceSq)
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
        CompleteCurrentTask(detail);
        UpdateExtinguishDebugStage(ExtinguishDebugStage.Completed, detail);
        ClearExtinguishRuntimeState();
        behaviorContext.ClearExtinguishOrder();
        navMeshAgent.isStopped = false;
    }

    private void ClearExtinguishRuntimeState()
    {
        ClearHeadAimFocus();
        ClearHandAimFocus();
        ResetExtinguishCrouchState();
        StopExtinguisher();
        ClearExtinguisherTargetLock();
        SetPickupWindow(false);
        ReleaseCommittedTool();
        preferredExtinguishTool = null;
        SetCurrentFireTarget(null);
        commandedPointFireTarget = null;
        commandedFireGroupTarget = null;
        currentExtinguishTargetPosition = default;
        currentExtinguishAimPoint = default;
        currentExtinguishLaunchDirection = default;
        hasCurrentExtinguishTargetPosition = false;
        hasCurrentExtinguishAimPoint = false;
        hasCurrentExtinguishLaunchDirection = false;
        currentExtinguishTrajectoryPointCount = 0;
        extinguishStartupPending = false;
        sprayReadyTime = -1f;
        activityDebug?.ResetExtinguish();
    }
}
