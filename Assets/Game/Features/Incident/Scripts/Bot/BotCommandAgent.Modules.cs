using TrueJourney.BotBehavior;
using UnityEngine;
using UnityEngine.AI;

public partial class BotCommandAgent
{
    private BotCommandExecutionModule commandExecutionModule;
    private BotCommandNavigationModule navigationModule;

    private sealed class BotCommandExecutionModule
    {
        private readonly BotCommandAgent owner;

        public BotCommandExecutionModule(BotCommandAgent owner)
        {
            this.owner = owner;
        }

        public bool CanAcceptCommand(BotCommandType commandType)
        {
            if (owner.navMeshAgent == null || !owner.navMeshAgent.enabled || !owner.isActiveAndEnabled)
            {
                return false;
            }

            switch (commandType)
            {
                case BotCommandType.Move:
                    return true;
                case BotCommandType.Extinguish:
                    return owner.behaviorContext != null && owner.inventorySystem != null;
                case BotCommandType.Follow:
                case BotCommandType.Assist:
                case BotCommandType.Rescue:
                case BotCommandType.Hold:
                case BotCommandType.Breach:
                case BotCommandType.Isolate:
                case BotCommandType.Search:
                case BotCommandType.Regroup:
                    return owner.behaviorContext != null;
                default:
                    return false;
            }
        }

        public bool CanAcceptCommandIntent(BotCommandIntentPayload payload)
        {
            return payload.IsValid && CanAcceptCommand(payload.CommandType);
        }

        public bool TryIssueCommand(BotCommandType commandType, Vector3 worldPoint)
        {
            return TryIssueCommandIntent(BotCommandIntentPayload.Create(commandType, worldPoint));
        }

        public bool TryIssueCommandIntent(BotCommandIntentPayload payload)
        {
            if (!CanAcceptCommandIntent(payload))
            {
                return false;
            }

            if (!owner.navMeshAgent.isOnNavMesh)
            {
                return false;
            }

            BotCommandType commandType = payload.CommandType;
            Vector3 destination = payload.HasWorldPoint ? payload.WorldPoint : owner.transform.position;
            if (owner.navMeshSampleDistance > 0f &&
                NavMesh.SamplePosition(destination, out NavMeshHit navMeshHit, owner.navMeshSampleDistance, owner.navMeshAgent.areaMask))
            {
                destination = navMeshHit.position;
            }

            bool accepted;
            switch (commandType)
            {
                case BotCommandType.Move:
                    if (owner.behaviorContext != null && owner.behaviorContext.UseMoveOrdersAsBehaviorInput)
                    {
                        PrepareForIssuedCommand(BotCommandType.Move);
                        owner.behaviorContext.SetMoveOrder(destination);
                        accepted = true;
                    }
                    else
                    {
                        owner.behaviorContext?.SetCommandIntent(BotCommandIntentPayload.Create(BotCommandType.Move, destination));
                        owner.navMeshAgent.isStopped = false;
                        accepted = owner.navMeshAgent.SetDestination(destination);
                    }
                    break;
                case BotCommandType.Extinguish:
                    return TryIssueExtinguishCommand(destination, payload.ExtinguishMode, payload.ExtinguishEngagementMode);
                case BotCommandType.Follow:
                    if (owner.behaviorContext == null)
                    {
                        return false;
                    }

                    PrepareForIssuedCommand(BotCommandType.Follow);
                    owner.behaviorContext.SetFollowOrder(owner.CreateFollowOrder(BotCommandType.Follow));
                    accepted = true;
                    break;
                case BotCommandType.Assist:
                    if (owner.behaviorContext == null)
                    {
                        return false;
                    }

                    PrepareForIssuedCommand(BotCommandType.Assist);
                    owner.behaviorContext.SetAssistOrder(owner.CreateFollowOrder(BotCommandType.Assist));
                    accepted = true;
                    break;
                case BotCommandType.Rescue:
                    if (owner.behaviorContext == null)
                    {
                        return false;
                    }

                    PrepareForIssuedCommand(BotCommandType.Rescue);
                    owner.behaviorContext.SetRescueOrder(destination);
                    accepted = true;
                    break;
                case BotCommandType.Hold:
                    PrepareForIssuedCommand(BotCommandType.Hold);
                    owner.behaviorContext.SetHoldOrder(owner.transform.position);
                    owner.navMeshAgent.ResetPath();
                    owner.navMeshAgent.isStopped = true;
                    destination = owner.transform.position;
                    accepted = true;
                    break;
                case BotCommandType.Breach:
                    PrepareForIssuedCommand(BotCommandType.Breach);
                    if (owner.behaviorContext.UseMoveOrdersAsBehaviorInput)
                    {
                        owner.behaviorContext.SetBreachOrder(destination);
                        accepted = true;
                    }
                    else
                    {
                        owner.behaviorContext.SetCommandIntent(BotCommandIntentPayload.Create(BotCommandType.Breach, destination));
                        owner.navMeshAgent.isStopped = false;
                        accepted = owner.navMeshAgent.SetDestination(destination);
                    }
                    break;
                case BotCommandType.Isolate:
                    PrepareForIssuedCommand(BotCommandType.Isolate);
                    if (owner.behaviorContext.UseMoveOrdersAsBehaviorInput)
                    {
                        owner.behaviorContext.SetIsolateOrder(destination);
                        accepted = true;
                    }
                    else
                    {
                        owner.behaviorContext.SetCommandIntent(BotCommandIntentPayload.Create(BotCommandType.Isolate, destination));
                        owner.navMeshAgent.isStopped = false;
                        accepted = owner.navMeshAgent.SetDestination(destination);
                    }
                    break;
                case BotCommandType.Search:
                    PrepareForIssuedCommand(BotCommandType.Search);
                    if (owner.behaviorContext.UseMoveOrdersAsBehaviorInput)
                    {
                        owner.behaviorContext.SetSearchOrder(destination);
                        accepted = true;
                    }
                    else
                    {
                        owner.behaviorContext.SetCommandIntent(BotCommandIntentPayload.Create(BotCommandType.Search, destination));
                        owner.navMeshAgent.isStopped = false;
                        accepted = owner.navMeshAgent.SetDestination(destination);
                    }
                    break;
                case BotCommandType.Regroup:
                    PrepareForIssuedCommand(BotCommandType.Regroup);
                    owner.behaviorContext.SetRegroupOrder(owner.CreateFollowOrder(BotCommandType.Regroup));
                    accepted = true;
                    break;
                default:
                    return false;
            }

            if (!accepted)
            {
                return false;
            }

            owner.lastIssuedDestination = destination;
            owner.hasIssuedDestination = true;
            return true;
        }

        public bool TryIssueExtinguishCommand(
            Vector3 scanOrigin,
            BotExtinguishCommandMode mode,
            BotExtinguishEngagementMode engagementMode,
            IFireTarget pointFireTarget = null,
            IFireGroupTarget fireGroupTarget = null)
        {
            if (!CanAcceptCommand(BotCommandType.Extinguish) || owner.behaviorContext == null || !owner.navMeshAgent.isOnNavMesh)
            {
                return false;
            }

            if (engagementMode == BotExtinguishEngagementMode.PrecisionFireHose &&
                (fireGroupTarget == null || !fireGroupTarget.HasActiveFires))
            {
                return false;
            }

            Vector3 approachDestination = scanOrigin;
            if (engagementMode == BotExtinguishEngagementMode.DirectBestTool &&
                mode == BotExtinguishCommandMode.PointFire &&
                owner.TryResolvePointFireApproachPosition(scanOrigin, out Vector3 sampledDestination))
            {
                approachDestination = sampledDestination;
            }
            else if (owner.navMeshSampleDistance > 0f &&
                     NavMesh.SamplePosition(scanOrigin, out NavMeshHit navMeshHit, owner.navMeshSampleDistance, owner.navMeshAgent.areaMask))
            {
                approachDestination = navMeshHit.position;
            }

            PrepareForIssuedCommand(BotCommandType.Extinguish);
            owner.behaviorContext.ClearExtinguishOrder();
            owner.ClearExtinguishRuntimeState();
            owner.BeginExtinguishV2Order(approachDestination, scanOrigin, mode, engagementMode, pointFireTarget, fireGroupTarget);
            owner.behaviorContext.SetCommandIntent(BotCommandIntentPayload.CreateExtinguish(approachDestination, scanOrigin, mode, engagementMode));
            owner.lastIssuedDestination = approachDestination;
            owner.hasIssuedDestination = true;
            return true;
        }

        public void PrepareForIssuedCommand(BotCommandType commandType)
        {
            if (owner.behaviorContext == null)
            {
                return;
            }

            owner.ClearSuspendedFollowResume();
            owner.behaviorContext.ClearOrdersExcept(commandType);

            if (commandType != BotCommandType.Extinguish && owner.activityDebug != null && owner.activityDebug.HasExtinguishDebugStage)
            {
                owner.ClearExtinguishRuntimeState();
            }

            if (commandType != BotCommandType.Rescue && !owner.ShouldPreserveRescueRuntimeState())
            {
                owner.ClearRescueRuntimeState();
            }

            if (commandType != BotCommandType.Breach)
            {
                owner.ClearBreachRuntimeState();
            }

            if (commandType != BotCommandType.Isolate)
            {
                owner.ClearHazardIsolationRuntimeState();
            }

            if (!BotCommandTypeUtility.UsesMoveOrder(commandType))
            {
                owner.ResetMoveActivityDebug();
            }

            if (!BotCommandTypeUtility.UsesFollowOrder(commandType))
            {
                owner.followTarget = null;
                owner.lastFollowDestination = Vector3.zero;
                owner.currentEscortSlotIndex = -1;
            }

            owner.ClearBlockedPathRuntime();
            owner.ClearRouteFireRuntime();
        }
    }

    private sealed class BotCommandNavigationModule
    {
        private readonly BotCommandAgent owner;

        public BotCommandNavigationModule(BotCommandAgent owner)
        {
            this.owner = owner;
        }

        public bool TryNavigateTo(Vector3 destination)
        {
            return TryNavigateTo(destination, true, true);
        }

        public bool TryNavigateTo(Vector3 destination, bool allowBlockedPathInterrupt, bool allowRouteFireInterrupt)
        {
            if (!owner.suppressPathFlowLogging)
            {
                owner.LogPathClearingFlow(
                    $"move-destination:{BotCommandAgent.FormatFlowVectorKey(destination)}",
                    $"Received Move order to {destination}.");
            }

            bool activatedSafeMovementObstacle = owner.TryRefreshSafeMovementObstacles(destination, false);

            if (allowBlockedPathInterrupt && owner.TryHandleBlockedPath(destination))
            {
                return true;
            }

            if (!activatedSafeMovementObstacle &&
                allowRouteFireInterrupt &&
                owner.TryHandleRouteBlockingFire(destination))
            {
                return true;
            }

            owner.navMeshAgent.isStopped = false;
            if (owner.navMeshSampleDistance > 0f &&
                NavMesh.SamplePosition(destination, out NavMeshHit navMeshHit, owner.navMeshSampleDistance, owner.navMeshAgent.areaMask))
            {
                destination = navMeshHit.position;
            }

            if (!owner.suppressPathFlowLogging)
            {
                owner.LogPathClearingFlow(
                    $"move-start:{BotCommandAgent.FormatFlowVectorKey(destination)}",
                    "Moving.");
            }
            return owner.navMeshAgent.SetDestination(destination);
        }

        public bool TrySetDestinationDirect(Vector3 destination)
        {
            if (owner.navMeshAgent == null || !owner.navMeshAgent.enabled || !owner.navMeshAgent.isOnNavMesh)
            {
                return false;
            }

            owner.navMeshAgent.isStopped = false;
            if (owner.navMeshSampleDistance > 0f &&
                NavMesh.SamplePosition(destination, out NavMeshHit navMeshHit, owner.navMeshSampleDistance, owner.navMeshAgent.areaMask))
            {
                destination = navMeshHit.position;
            }

            owner.TryRefreshSafeMovementObstacles(destination, false);
            return owner.navMeshAgent.SetDestination(destination);
        }

        public bool TryCalculatePreviewPath(Vector3 destination, out Vector3 sampledDestination, out NavMeshPath previewPath)
        {
            sampledDestination = destination;
            previewPath = null;

            if (owner.navMeshAgent == null || !owner.navMeshAgent.enabled || !owner.navMeshAgent.isOnNavMesh)
            {
                return false;
            }

            if (owner.navMeshSampleDistance > 0f &&
                NavMesh.SamplePosition(destination, out NavMeshHit navMeshHit, owner.navMeshSampleDistance, owner.navMeshAgent.areaMask))
            {
                sampledDestination = navMeshHit.position;
            }

            previewPath = new NavMeshPath();
            return NavMesh.CalculatePath(owner.transform.position, sampledDestination, owner.navMeshAgent.areaMask, previewPath);
        }

        public bool ShouldRefreshPathClearingCheck()
        {
            if (owner.navMeshAgent == null || !owner.navMeshAgent.enabled || !owner.navMeshAgent.isOnNavMesh)
            {
                return false;
            }

            bool shouldRefreshForBreakables = owner.enablePathClearing;
            bool shouldRefreshForRouteFire = owner.enableRouteFireClearing && !owner.enableSafeMovement;
            bool shouldRefreshForSafeMovement = owner.enableSafeMovement;
            if (!shouldRefreshForBreakables && !shouldRefreshForRouteFire && !shouldRefreshForSafeMovement)
            {
                return false;
            }

            if (shouldRefreshForBreakables &&
                owner.currentBlockedBreakable != null &&
                !owner.currentBlockedBreakable.IsBroken &&
                owner.currentBlockedBreakable.CanBeClearedByBot)
            {
                return true;
            }

            if (Time.time < owner.nextPathClearingRefreshTime)
            {
                return false;
            }

            owner.nextPathClearingRefreshTime = Time.time + Mathf.Max(0.05f, owner.pathClearingRefreshInterval);
            return true;
        }

        public bool MoveTo(Vector3 destination)
        {
            return owner.pathClearingController != null
                ? owner.pathClearingController.TryNavigateTo(destination)
                : TryNavigateTo(destination);
        }

        public bool TryTraverseOffMeshLink()
        {
            if (owner.navMeshAgent == null || !owner.navMeshAgent.enabled)
            {
                return false;
            }

            owner.navMeshAgent.autoTraverseOffMeshLink = !owner.enableManualOffMeshTraversal;
            if (!owner.enableManualOffMeshTraversal || !owner.navMeshAgent.isOnNavMesh || !owner.navMeshAgent.isOnOffMeshLink)
            {
                return false;
            }

            OffMeshLinkData offMeshLinkData = owner.navMeshAgent.currentOffMeshLinkData;
            Vector3 endPosition = offMeshLinkData.endPos + Vector3.up * owner.navMeshAgent.baseOffset;
            float moveSpeed = Mathf.Max(0.1f, owner.offMeshTraverseSpeed);
            float arrivalDistance = Mathf.Max(0.01f, owner.offMeshArrivalDistance);

            owner.transform.position = Vector3.MoveTowards(owner.transform.position, endPosition, moveSpeed * Time.deltaTime);
            AimTowards(endPosition);

            if ((endPosition - owner.transform.position).sqrMagnitude > arrivalDistance * arrivalDistance)
            {
                return true;
            }

            owner.transform.position = endPosition;
            owner.navMeshAgent.CompleteOffMeshLink();
            return true;
        }

        public void CacheMovementSpeedDefaults()
        {
            if (owner.movementSpeedDefaultsCached)
            {
                return;
            }

            owner.baseOffMeshTraverseSpeed = owner.offMeshTraverseSpeed;
            if (owner.navMeshAgent != null)
            {
                owner.baseNavMeshAgentSpeed = owner.navMeshAgent.speed;
            }

            owner.movementSpeedDefaultsCached = true;
        }

        public void RestoreMovementSpeedDefaults()
        {
            if (!owner.movementSpeedDefaultsCached)
            {
                return;
            }

            owner.offMeshTraverseSpeed = owner.baseOffMeshTraverseSpeed;
            if (owner.navMeshAgent != null)
            {
                owner.navMeshAgent.speed = owner.baseNavMeshAgentSpeed;
            }
        }

        public void UpdateCarryMovementSpeed()
        {
            CacheMovementSpeedDefaults();

            float speedMultiplier = EvaluateCarryMovementSpeedMultiplier();
            owner.offMeshTraverseSpeed = owner.baseOffMeshTraverseSpeed * Mathf.Lerp(1f, speedMultiplier, Mathf.Clamp01(owner.carryOffMeshPenaltyScale));

            if (owner.navMeshAgent != null)
            {
                owner.navMeshAgent.speed = owner.baseNavMeshAgentSpeed * speedMultiplier;
            }
        }

        public float EvaluateCarryMovementSpeedMultiplier()
        {
            if (!owner.applyCarryWeightSpeedPenalty)
            {
                return 1f;
            }

            float carriedWeightKg = ResolveCurrentCarryWeightKg();
            if (carriedWeightKg <= 0f)
            {
                return 1f;
            }

            float targetMinimumMultiplier = Mathf.Clamp(owner.minimumCarrySpeedMultiplier, 0.05f, 1f);
            if (owner.carryWeightForMinimumSpeed <= 0f)
            {
                return targetMinimumMultiplier;
            }

            float burdenT = Mathf.Clamp01(carriedWeightKg / owner.carryWeightForMinimumSpeed);
            return Mathf.Lerp(1f, targetMinimumMultiplier, burdenT);
        }

        public float ResolveCurrentCarryWeightKg()
        {
            IRescuableTarget carriedTarget = ResolveCarriedRescueTarget();
            if (carriedTarget == null)
            {
                return 0f;
            }

            if (!(carriedTarget is Component component))
            {
                return 0f;
            }

            IMovementWeightSource weightSource = component.GetComponent<IMovementWeightSource>();
            if (weightSource != null)
            {
                return Mathf.Max(0f, weightSource.MovementWeightKg);
            }

            Rigidbody fallbackBody = component.GetComponent<Rigidbody>();
            return fallbackBody != null
                ? Mathf.Max(0f, fallbackBody.mass)
                : 0f;
        }

        public bool ShouldPreserveRescueRuntimeState()
        {
            return ResolveCarriedRescueTarget() != null;
        }

        public IRescuableTarget ResolveCarriedRescueTarget()
        {
            if (IsOwnedCarriedRescueTarget(owner.currentRescueTarget))
            {
                return owner.currentRescueTarget;
            }

            foreach (IRescuableTarget candidate in BotRuntimeRegistry.ActiveRescuableTargets)
            {
                if (!IsOwnedCarriedRescueTarget(candidate))
                {
                    continue;
                }

                owner.SetCurrentRescueTarget(candidate);
                return candidate;
            }

            return null;
        }

        public bool IsWithinArrivalDistance(Vector3 destination)
        {
            float threshold = (owner.behaviorContext != null ? owner.behaviorContext.ArrivalDistance : 0.35f) + 0.2f;
            if (owner.navMeshAgent != null &&
                owner.navMeshAgent.enabled &&
                owner.navMeshAgent.isOnNavMesh &&
                owner.navMeshAgent.hasPath &&
                !owner.navMeshAgent.pathPending)
            {
                float remainingDistance = owner.navMeshAgent.remainingDistance;
                if (!float.IsInfinity(remainingDistance) && remainingDistance <= threshold)
                {
                    return true;
                }
            }

            Vector2 destination2 = new Vector2(destination.x, destination.z);
            Vector2 current2 = new Vector2(owner.transform.position.x, owner.transform.position.z);
            return (destination2 - current2).sqrMagnitude <= threshold * threshold;
        }

        public void AimTowards(Vector3 worldPoint)
        {
            Vector3 yawDirection = worldPoint - owner.transform.position;
            yawDirection.y = 0f;
            if (yawDirection.sqrMagnitude >= 0.001f)
            {
                Quaternion targetYaw = Quaternion.LookRotation(yawDirection.normalized, Vector3.up);
                owner.transform.rotation = Quaternion.RotateTowards(owner.transform.rotation, targetYaw, owner.turnSpeed * Time.deltaTime);
            }
        }

        private bool IsOwnedCarriedRescueTarget(IRescuableTarget candidate)
        {
            return candidate != null &&
                   candidate.IsCarried &&
                   candidate.ActiveRescuer == owner.gameObject;
        }
    }
}
