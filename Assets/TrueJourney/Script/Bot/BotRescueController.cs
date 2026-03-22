using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.AI;

public sealed class BotRescueController
{
    private readonly BotRuntimeDecisionService decisionService;

    public BotRescueController(BotRuntimeDecisionService decisionService)
    {
        this.decisionService = decisionService;
    }

    public void Tick(
        BotCommandAgent owner,
        NavMeshAgent navMeshAgent,
        BotBehaviorContext behaviorContext,
        float rescueSearchRadius,
        float rescueInteractionDistance,
        float rescueSafeZoneArrivalDistance,
        Vector3 rescueDropOffset)
    {
        if (navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh ||
            behaviorContext == null ||
            !behaviorContext.TryGetRescueOrder(out Vector3 orderPoint))
        {
            return;
        }

        owner.LogRescueActivityMessage(
            $"rescue-order:{owner.FormatFlowVectorKeyForLog(orderPoint)}",
            $"Nhận lệnh Rescue tới {orderPoint}.");

        IRescuableTarget rescueTarget = decisionService.ResolveRescueTarget(orderPoint, owner.CurrentRescueTarget, owner.gameObject, rescueSearchRadius);
        if (rescueTarget == null)
        {
            owner.LogRescueActivityMessage("rescue-notfound", "Không thấy người cần cứu.");
            owner.AbortActiveRescueOrder();
            return;
        }

        owner.CurrentRescueTarget = rescueTarget;
        owner.CurrentSafeZoneTarget = decisionService.ResolveNearestSafeZone(rescueTarget.GetWorldPosition(), owner.CurrentSafeZoneTarget);

        if (!rescueTarget.NeedsRescue)
        {
            owner.LogRescueActivityMessage("rescue-complete", "Hoàn tất cứu.");
            owner.CompleteActiveRescueOrder();
            return;
        }

        if (owner.CurrentSafeZoneTarget == null)
        {
            owner.LogRescueActivityMessage("rescue-no-safezone", "Không thấy vùng an toàn.");
            owner.AbortActiveRescueOrder();
            return;
        }

        if (rescueTarget.IsCarried && rescueTarget.ActiveRescuer == owner.gameObject)
        {
            Vector3 safeZonePosition = owner.CurrentSafeZoneTarget.GetWorldPosition();
            float distanceToSafeZone = GetHorizontalDistance(owner.transform.position, safeZonePosition);
            bool hasReachedSafeZone =
                owner.CurrentSafeZoneTarget.ContainsPoint(owner.transform.position) ||
                distanceToSafeZone <= rescueSafeZoneArrivalDistance;

            if (!hasReachedSafeZone)
            {
                owner.LogRescueActivityMessage("rescue-carry", "Mang nạn nhân tới vùng an toàn.");
                owner.MoveToCommand(safeZonePosition);
                return;
            }

            navMeshAgent.ResetPath();
            navMeshAgent.isStopped = true;
            Vector3 fallbackDropPosition = owner.transform.position + owner.transform.TransformDirection(rescueDropOffset);
            Vector3 dropPosition = owner.CurrentSafeZoneTarget.GetDropPoint(fallbackDropPosition);
            rescueTarget.CompleteRescueAt(dropPosition);
            owner.LogRescueActivityMessage("rescue-complete", "Hoàn tất cứu.");
            owner.CompleteActiveRescueOrder();
            return;
        }

        Vector3 targetPosition = rescueTarget.GetWorldPosition();
        float horizontalDistance = GetHorizontalDistance(owner.transform.position, targetPosition);
        if (horizontalDistance > rescueInteractionDistance)
        {
            owner.LogRescueActivityMessage("rescue-move", "Di chuyển tới người bị nạn.");
            owner.MoveToCommand(targetPosition);
            return;
        }

        navMeshAgent.ResetPath();
        navMeshAgent.isStopped = true;
        owner.AimTowardsPoint(targetPosition);

        if (rescueTarget.IsRescueInProgress && rescueTarget.ActiveRescuer == owner.gameObject)
        {
            owner.LogRescueActivityMessage("rescue-start", "Bắt đầu cứu.");
            return;
        }

        if (rescueTarget.TryBeginCarry(owner.gameObject, owner.EnsureRescueCarryAnchor()))
        {
            owner.LogRescueActivityMessage("rescue-pickup", "Nhấc nạn nhân.");
            return;
        }

        owner.CurrentRescueTarget = null;
    }

    private static float GetHorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
