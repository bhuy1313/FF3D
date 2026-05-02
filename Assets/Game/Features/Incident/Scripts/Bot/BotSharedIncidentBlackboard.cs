using System.Collections.Generic;
using UnityEngine;

namespace TrueJourney.BotBehavior
{
    public sealed class BotSharedIncidentBlackboard
    {
        private sealed class RescueRecord
        {
            public IRescuableTarget Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
            public float Priority;
        }

        private sealed class FireRecord
        {
            public IFireTarget Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
        }

        private sealed class FireGroupRecord
        {
            public IFireGroupTarget Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
        }

        private sealed class BreakableRecord
        {
            public IBotBreakableTarget Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
        }

        private sealed class ExtinguisherRecord
        {
            public IBotExtinguisherItem Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
        }

        private sealed class BreakToolRecord
        {
            public IBotBreakTool Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
        }

        private sealed class HazardIsolationRecord
        {
            public IBotHazardIsolationTarget Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
        }

        private sealed class FollowTargetRecord
        {
            public Transform Target;
            public float LastSeenTime;
        }

        private readonly Dictionary<IRescuableTarget, RescueRecord> rescueRecords = new Dictionary<IRescuableTarget, RescueRecord>();
        private readonly Dictionary<IFireTarget, FireRecord> fireRecords = new Dictionary<IFireTarget, FireRecord>();
        private readonly Dictionary<IFireGroupTarget, FireGroupRecord> fireGroupRecords = new Dictionary<IFireGroupTarget, FireGroupRecord>();
        private readonly Dictionary<IBotBreakableTarget, BreakableRecord> breakableRecords = new Dictionary<IBotBreakableTarget, BreakableRecord>();
        private readonly Dictionary<IBotExtinguisherItem, ExtinguisherRecord> extinguisherRecords = new Dictionary<IBotExtinguisherItem, ExtinguisherRecord>();
        private readonly Dictionary<IBotBreakTool, BreakToolRecord> breakToolRecords = new Dictionary<IBotBreakTool, BreakToolRecord>();
        private readonly Dictionary<IBotHazardIsolationTarget, HazardIsolationRecord> hazardIsolationRecords = new Dictionary<IBotHazardIsolationTarget, HazardIsolationRecord>();
        private readonly Dictionary<string, FollowTargetRecord> followTargetRecords = new Dictionary<string, FollowTargetRecord>();

        private const float RescueMemorySeconds = 20f;
        private const float FireMemorySeconds = 10f;
        private const float BreakableMemorySeconds = 15f;
        private const float ExtinguisherMemorySeconds = 15f;
        private const float BreakToolMemorySeconds = 15f;
        private const float HazardIsolationMemorySeconds = 20f;
        private const float FollowTargetMemorySeconds = 20f;

        public void RememberRescuable(IRescuableTarget target)
        {
            if (IsUnityObjectDestroyed(target))
            {
                return;
            }

            CleanupExpiredRescueRecords();
            if (!rescueRecords.TryGetValue(target, out RescueRecord record) || record == null)
            {
                record = new RescueRecord
                {
                    Target = target
                };
                rescueRecords[target] = record;
            }

            record.LastKnownPosition = target.GetWorldPosition();
            record.LastSeenTime = Time.time;
            record.Priority = target.RescuePriority;
        }

        public bool TryGetBestRecentRescueTarget(Vector3 orderPoint, float rescueSearchRadius, GameObject requester, out IRescuableTarget target)
        {
            CleanupExpiredRescueRecords();

            target = null;
            float bestDistance = float.MaxValue;
            float bestPriority = float.NegativeInfinity;

            foreach (KeyValuePair<IRescuableTarget, RescueRecord> pair in rescueRecords)
            {
                RescueRecord record = pair.Value;
                IRescuableTarget candidate = record?.Target;
                if (IsUnityObjectDestroyed(candidate) || !candidate.NeedsRescue)
                {
                    continue;
                }

                if (candidate.IsRescueInProgress && candidate.ActiveRescuer != requester)
                {
                    continue;
                }

                if (BotRuntimeRegistry.Reservations.IsReservedByOther(candidate, requester))
                {
                    continue;
                }

                float candidateDistance = GetHorizontalDistance(orderPoint, candidate.GetWorldPosition());
                if (candidateDistance > rescueSearchRadius)
                {
                    continue;
                }

                float candidatePriority = candidate.RescuePriority;
                bool isBetterPriority = candidatePriority > bestPriority;
                bool isTieBreakerDistance = Mathf.Approximately(candidatePriority, bestPriority) && candidateDistance < bestDistance;
                if (!isBetterPriority && !isTieBreakerDistance)
                {
                    continue;
                }

                bestDistance = candidateDistance;
                bestPriority = candidatePriority;
                target = candidate;
            }

            return target != null;
        }

        public void RememberFire(IFireTarget target)
        {
            if (IsUnityObjectDestroyed(target))
            {
                return;
            }

            CleanupExpiredFireRecords();
            if (!fireRecords.TryGetValue(target, out FireRecord record) || record == null)
            {
                record = new FireRecord
                {
                    Target = target
                };
                fireRecords[target] = record;
            }

            record.LastKnownPosition = target.GetWorldPosition();
            record.LastSeenTime = Time.time;
        }

        public void RememberFireGroup(IFireGroupTarget target, Vector3 referencePosition)
        {
            if (IsUnityObjectDestroyed(target) || target == null || !target.HasActiveFires)
            {
                return;
            }

            CleanupExpiredFireGroupRecords();
            if (!fireGroupRecords.TryGetValue(target, out FireGroupRecord record) || record == null)
            {
                record = new FireGroupRecord
                {
                    Target = target
                };
                fireGroupRecords[target] = record;
            }

            record.LastKnownPosition = target.GetClosestActiveFirePosition(referencePosition);
            record.LastSeenTime = Time.time;
        }

        public bool TryGetNearestRecentFire(Vector3 fromPosition, float maxDistance, out IFireTarget target)
        {
            CleanupExpiredFireRecords();

            target = null;
            float bestDistanceSq = float.PositiveInfinity;
            float maxDistanceSq = Mathf.Max(0.05f, maxDistance) * Mathf.Max(0.05f, maxDistance);

            foreach (KeyValuePair<IFireTarget, FireRecord> pair in fireRecords)
            {
                IFireTarget candidate = pair.Value?.Target;
                if (IsUnityObjectDestroyed(candidate) || !candidate.IsBurning)
                {
                    continue;
                }

                float distanceSq = (candidate.GetWorldPosition() - fromPosition).sqrMagnitude;
                if (distanceSq > maxDistanceSq || distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                target = candidate;
            }

            return target != null;
        }

        public bool TryGetNearestRecentFireGroup(Vector3 fromPosition, float maxDistance, out IFireGroupTarget target)
        {
            CleanupExpiredFireGroupRecords();

            target = null;
            float bestDistanceSq = float.PositiveInfinity;
            float maxDistanceSq = Mathf.Max(0.05f, maxDistance) * Mathf.Max(0.05f, maxDistance);

            foreach (KeyValuePair<IFireGroupTarget, FireGroupRecord> pair in fireGroupRecords)
            {
                IFireGroupTarget candidate = pair.Value?.Target;
                if (IsUnityObjectDestroyed(candidate) || candidate == null || !candidate.HasActiveFires)
                {
                    continue;
                }

                Vector3 candidatePosition = candidate.GetClosestActiveFirePosition(fromPosition);
                float distanceSq = (candidatePosition - fromPosition).sqrMagnitude;
                if (distanceSq > maxDistanceSq || distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                target = candidate;
            }

            return target != null;
        }

        public void RememberBreakable(IBotBreakableTarget target)
        {
            if (IsUnityObjectDestroyed(target))
            {
                return;
            }

            CleanupExpiredBreakableRecords();
            if (!breakableRecords.TryGetValue(target, out BreakableRecord record) || record == null)
            {
                record = new BreakableRecord
                {
                    Target = target
                };
                breakableRecords[target] = record;
            }

            record.LastKnownPosition = target.GetWorldPosition();
            record.LastSeenTime = Time.time;
        }

        public void RememberExtinguisher(IBotExtinguisherItem target)
        {
            if (IsUnityObjectDestroyed(target))
            {
                return;
            }

            CleanupExpiredExtinguisherRecords();
            if (!extinguisherRecords.TryGetValue(target, out ExtinguisherRecord record) || record == null)
            {
                record = new ExtinguisherRecord
                {
                    Target = target
                };
                extinguisherRecords[target] = record;
            }

            record.LastKnownPosition = GetToolWorldPosition(target);
            record.LastSeenTime = Time.time;
        }

        public bool TryGetNearestRecentExtinguisher(Vector3 fromPosition, float maxDistance, GameObject requester, out IBotExtinguisherItem target)
        {
            CleanupExpiredExtinguisherRecords();

            target = null;
            float bestDistanceSq = float.PositiveInfinity;
            float maxDistanceSq = Mathf.Max(0.05f, maxDistance) * Mathf.Max(0.05f, maxDistance);

            foreach (KeyValuePair<IBotExtinguisherItem, ExtinguisherRecord> pair in extinguisherRecords)
            {
                IBotExtinguisherItem candidate = pair.Value?.Target;
                if (IsUnityObjectDestroyed(candidate) || candidate.IsHeld || !candidate.HasUsableCharge || !candidate.IsAvailableTo(requester))
                {
                    continue;
                }

                float distanceSq = (GetToolWorldPosition(candidate) - fromPosition).sqrMagnitude;
                if (distanceSq > maxDistanceSq || distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                target = candidate;
            }

            return target != null;
        }

        public void RememberBreakTool(IBotBreakTool target)
        {
            if (IsUnityObjectDestroyed(target))
            {
                return;
            }

            CleanupExpiredBreakToolRecords();
            if (!breakToolRecords.TryGetValue(target, out BreakToolRecord record) || record == null)
            {
                record = new BreakToolRecord
                {
                    Target = target
                };
                breakToolRecords[target] = record;
            }

            record.LastKnownPosition = GetToolWorldPosition(target);
            record.LastSeenTime = Time.time;
        }

        public bool TryGetNearestRecentBreakTool(Vector3 fromPosition, float maxDistance, GameObject requester, out IBotBreakTool target)
        {
            CleanupExpiredBreakToolRecords();

            target = null;
            float bestDistanceSq = float.PositiveInfinity;
            float maxDistanceSq = Mathf.Max(0.05f, maxDistance) * Mathf.Max(0.05f, maxDistance);

            foreach (KeyValuePair<IBotBreakTool, BreakToolRecord> pair in breakToolRecords)
            {
                IBotBreakTool candidate = pair.Value?.Target;
                if (IsUnityObjectDestroyed(candidate) || candidate.IsHeld || !candidate.IsAvailableTo(requester))
                {
                    continue;
                }

                float distanceSq = (GetToolWorldPosition(candidate) - fromPosition).sqrMagnitude;
                if (distanceSq > maxDistanceSq || distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                target = candidate;
            }

            return target != null;
        }

        public void RememberHazardIsolationTarget(IBotHazardIsolationTarget target)
        {
            if (IsUnityObjectDestroyed(target))
            {
                return;
            }

            CleanupExpiredHazardIsolationRecords();
            if (!hazardIsolationRecords.TryGetValue(target, out HazardIsolationRecord record) || record == null)
            {
                record = new HazardIsolationRecord
                {
                    Target = target
                };
                hazardIsolationRecords[target] = record;
            }

            record.LastKnownPosition = target.GetWorldPosition();
            record.LastSeenTime = Time.time;
        }

        public bool TryGetNearestRecentHazardIsolationTarget(Vector3 fromPosition, float maxDistance, FireHazardType hazardType, out IBotHazardIsolationTarget target)
        {
            return TryGetNearestRecentHazardIsolationTarget(fromPosition, maxDistance, out target, hazardType);
        }

        public bool TryGetNearestRecentHazardIsolationTarget(Vector3 fromPosition, float maxDistance, out IBotHazardIsolationTarget target)
        {
            return TryGetNearestRecentHazardIsolationTarget(fromPosition, maxDistance, out target, null);
        }

        private bool TryGetNearestRecentHazardIsolationTarget(
            Vector3 fromPosition,
            float maxDistance,
            out IBotHazardIsolationTarget target,
            FireHazardType? hazardType)
        {
            CleanupExpiredHazardIsolationRecords();

            target = null;
            float bestDistanceSq = float.PositiveInfinity;
            float maxDistanceSq = Mathf.Max(0.05f, maxDistance) * Mathf.Max(0.05f, maxDistance);

            foreach (KeyValuePair<IBotHazardIsolationTarget, HazardIsolationRecord> pair in hazardIsolationRecords)
            {
                IBotHazardIsolationTarget candidate = pair.Value?.Target;
                if (IsUnityObjectDestroyed(candidate) ||
                    !candidate.IsHazardActive ||
                    !candidate.IsInteractionAvailable ||
                    (hazardType.HasValue && candidate.HazardType != hazardType.Value))
                {
                    continue;
                }

                float distanceSq = (candidate.GetWorldPosition() - fromPosition).sqrMagnitude;
                if (distanceSq > maxDistanceSq || distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                target = candidate;
            }

            return target != null;
        }

        public bool TryGetNearestRecentBreakable(Vector3 fromPosition, float maxDistance, GameObject requester, out IBotBreakableTarget target)
        {
            CleanupExpiredBreakableRecords();

            target = null;
            float bestDistanceSq = float.PositiveInfinity;
            float maxDistanceSq = Mathf.Max(0.05f, maxDistance) * Mathf.Max(0.05f, maxDistance);

            foreach (KeyValuePair<IBotBreakableTarget, BreakableRecord> pair in breakableRecords)
            {
                IBotBreakableTarget candidate = pair.Value?.Target;
                if (IsUnityObjectDestroyed(candidate) || candidate.IsBroken || !candidate.CanBeClearedByBot)
                {
                    continue;
                }

                if (BotRuntimeRegistry.Reservations.IsReservedByOther(candidate, requester))
                {
                    continue;
                }

                float distanceSq = (candidate.GetWorldPosition() - fromPosition).sqrMagnitude;
                if (distanceSq > maxDistanceSq || distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                target = candidate;
            }

            return target != null;
        }

        public void RememberFollowTarget(string targetTag, Transform target)
        {
            if (IsUnityObjectDestroyed(target) || string.IsNullOrWhiteSpace(targetTag))
            {
                return;
            }

            followTargetRecords[targetTag] = new FollowTargetRecord
            {
                Target = target,
                LastSeenTime = Time.time
            };
        }

        public bool TryGetRecentFollowTarget(string targetTag, out Transform target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(targetTag))
            {
                return false;
            }

            if (!followTargetRecords.TryGetValue(targetTag, out FollowTargetRecord record) || record == null)
            {
                return false;
            }

            if (IsUnityObjectDestroyed(record.Target) || !record.Target.gameObject.activeInHierarchy || IsExpired(record.LastSeenTime, FollowTargetMemorySeconds))
            {
                followTargetRecords.Remove(targetTag);
                return false;
            }

            target = record.Target;
            return true;
        }

        private void CleanupExpiredRescueRecords()
        {
            CleanupExpiredRecords(rescueRecords, RescueMemorySeconds, static record =>
                record == null || IsUnityObjectDestroyed(record.Target) || !record.Target.NeedsRescue, static record => record.LastSeenTime);
        }

        private void CleanupExpiredFireRecords()
        {
            CleanupExpiredRecords(fireRecords, FireMemorySeconds, static record =>
                record == null || IsUnityObjectDestroyed(record.Target) || !record.Target.IsBurning, static record => record.LastSeenTime);
        }

        private void CleanupExpiredFireGroupRecords()
        {
            CleanupExpiredRecords(fireGroupRecords, FireMemorySeconds, static record =>
                record == null || IsUnityObjectDestroyed(record.Target) || record.Target == null || !record.Target.HasActiveFires, static record => record.LastSeenTime);
        }

        private void CleanupExpiredBreakableRecords()
        {
            CleanupExpiredRecords(breakableRecords, BreakableMemorySeconds, static record =>
                record == null || IsUnityObjectDestroyed(record.Target) || record.Target.IsBroken || !record.Target.CanBeClearedByBot, static record => record.LastSeenTime);
        }

        private void CleanupExpiredExtinguisherRecords()
        {
            CleanupExpiredRecords(extinguisherRecords, ExtinguisherMemorySeconds, static record =>
                record == null || IsUnityObjectDestroyed(record.Target) || record.Target.IsHeld || !record.Target.HasUsableCharge, static record => record.LastSeenTime);
        }

        private void CleanupExpiredBreakToolRecords()
        {
            CleanupExpiredRecords(breakToolRecords, BreakToolMemorySeconds, static record =>
                record == null || IsUnityObjectDestroyed(record.Target) || record.Target.IsHeld, static record => record.LastSeenTime);
        }

        private void CleanupExpiredHazardIsolationRecords()
        {
            CleanupExpiredRecords(hazardIsolationRecords, HazardIsolationMemorySeconds, static record =>
                record == null || IsUnityObjectDestroyed(record.Target) || !record.Target.IsHazardActive, static record => record.LastSeenTime);
        }

        private static void CleanupExpiredRecords<TKey, TRecord>(
            Dictionary<TKey, TRecord> records,
            float memorySeconds,
            System.Func<TRecord, bool> invalidPredicate,
            System.Func<TRecord, float> timeSelector)
        {
            if (records.Count == 0)
            {
                return;
            }

            List<TKey> staleKeys = null;
            foreach (KeyValuePair<TKey, TRecord> pair in records)
            {
                if (invalidPredicate(pair.Value) || IsExpired(timeSelector(pair.Value), memorySeconds))
                {
                    staleKeys ??= new List<TKey>();
                    staleKeys.Add(pair.Key);
                }
            }

            if (staleKeys == null)
            {
                return;
            }

            for (int i = 0; i < staleKeys.Count; i++)
            {
                records.Remove(staleKeys[i]);
            }
        }

        private static bool IsExpired(float seenTime, float memorySeconds)
        {
            return seenTime < 0f || Time.time - seenTime > memorySeconds;
        }

        private static float GetHorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Vector3 GetToolWorldPosition(object target)
        {
            return target is Component component && component != null
                ? component.transform.position
                : Vector3.zero;
        }

        private static bool IsUnityObjectDestroyed(object value)
        {
            return value == null || (value is Object unityObject && unityObject == null);
        }
    }
}
