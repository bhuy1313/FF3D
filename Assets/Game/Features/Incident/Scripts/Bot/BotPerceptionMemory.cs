using System.Collections.Generic;
using UnityEngine;

namespace TrueJourney.BotBehavior
{
    [DisallowMultipleComponent]
    public sealed class BotPerceptionMemory : MonoBehaviour
    {
        [Header("Memory Durations")]
        [SerializeField] private float rescueMemorySeconds = 12f;
        [SerializeField] private float fireMemorySeconds = 8f;
        [SerializeField] private float breakableMemorySeconds = 10f;
        [SerializeField] private float extinguisherMemorySeconds = 12f;
        [SerializeField] private float breakToolMemorySeconds = 12f;
        [SerializeField] private float hazardIsolationMemorySeconds = 15f;
        [SerializeField] private float followTargetMemorySeconds = 15f;

        private sealed class RescueMemoryRecord
        {
            public IRescuableTarget Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
            public float Priority;
        }

        private sealed class FireMemoryRecord
        {
            public IFireTarget Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
        }

        private sealed class BreakableMemoryRecord
        {
            public IBotBreakableTarget Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
        }

        private sealed class ExtinguisherMemoryRecord
        {
            public IBotExtinguisherItem Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
        }

        private sealed class BreakToolMemoryRecord
        {
            public IBotBreakTool Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
        }

        private sealed class HazardIsolationMemoryRecord
        {
            public IBotHazardIsolationTarget Target;
            public Vector3 LastKnownPosition;
            public float LastSeenTime;
        }

        private readonly Dictionary<IRescuableTarget, RescueMemoryRecord> rescueRecords = new Dictionary<IRescuableTarget, RescueMemoryRecord>();
        private readonly Dictionary<IFireTarget, FireMemoryRecord> fireRecords = new Dictionary<IFireTarget, FireMemoryRecord>();
        private readonly Dictionary<IBotBreakableTarget, BreakableMemoryRecord> breakableRecords = new Dictionary<IBotBreakableTarget, BreakableMemoryRecord>();
        private readonly Dictionary<IBotExtinguisherItem, ExtinguisherMemoryRecord> extinguisherRecords = new Dictionary<IBotExtinguisherItem, ExtinguisherMemoryRecord>();
        private readonly Dictionary<IBotBreakTool, BreakToolMemoryRecord> breakToolRecords = new Dictionary<IBotBreakTool, BreakToolMemoryRecord>();
        private readonly Dictionary<IBotHazardIsolationTarget, HazardIsolationMemoryRecord> hazardIsolationRecords = new Dictionary<IBotHazardIsolationTarget, HazardIsolationMemoryRecord>();
        private Transform lastFollowTarget;
        private string lastFollowTargetTag = string.Empty;
        private float lastFollowTargetSeenTime = -1f;

        public void RememberRescuable(IRescuableTarget target)
        {
            if (IsUnityObjectDestroyed(target))
            {
                return;
            }

            CleanupExpiredRescueRecords();
            if (!rescueRecords.TryGetValue(target, out RescueMemoryRecord record) || record == null)
            {
                record = new RescueMemoryRecord
                {
                    Target = target
                };
                rescueRecords[target] = record;
            }

            record.LastKnownPosition = target.GetWorldPosition();
            record.LastSeenTime = Time.time;
            record.Priority = target.RescuePriority;
        }

        public bool TryGetBestRecentRescuable(Vector3 orderPoint, float rescueSearchRadius, GameObject requester, out IRescuableTarget target)
        {
            CleanupExpiredRescueRecords();

            target = null;
            float bestDistance = float.MaxValue;
            float bestPriority = float.NegativeInfinity;

            foreach (KeyValuePair<IRescuableTarget, RescueMemoryRecord> pair in rescueRecords)
            {
                RescueMemoryRecord record = pair.Value;
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
            if (!fireRecords.TryGetValue(target, out FireMemoryRecord record) || record == null)
            {
                record = new FireMemoryRecord
                {
                    Target = target
                };
                fireRecords[target] = record;
            }

            record.LastKnownPosition = target.GetWorldPosition();
            record.LastSeenTime = Time.time;
        }

        public bool TryGetNearestRecentFire(Vector3 fromPosition, float maxDistance, out IFireTarget target)
        {
            CleanupExpiredFireRecords();

            target = null;
            float bestDistanceSq = float.PositiveInfinity;
            float maxDistanceSq = Mathf.Max(0.05f, maxDistance) * Mathf.Max(0.05f, maxDistance);

            foreach (KeyValuePair<IFireTarget, FireMemoryRecord> pair in fireRecords)
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

        public void RememberBreakable(IBotBreakableTarget target)
        {
            if (IsUnityObjectDestroyed(target))
            {
                return;
            }

            CleanupExpiredBreakableRecords();
            if (!breakableRecords.TryGetValue(target, out BreakableMemoryRecord record) || record == null)
            {
                record = new BreakableMemoryRecord
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
            if (!extinguisherRecords.TryGetValue(target, out ExtinguisherMemoryRecord record) || record == null)
            {
                record = new ExtinguisherMemoryRecord
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

            foreach (KeyValuePair<IBotExtinguisherItem, ExtinguisherMemoryRecord> pair in extinguisherRecords)
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
            if (!breakToolRecords.TryGetValue(target, out BreakToolMemoryRecord record) || record == null)
            {
                record = new BreakToolMemoryRecord
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

            foreach (KeyValuePair<IBotBreakTool, BreakToolMemoryRecord> pair in breakToolRecords)
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
            if (!hazardIsolationRecords.TryGetValue(target, out HazardIsolationMemoryRecord record) || record == null)
            {
                record = new HazardIsolationMemoryRecord
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

            foreach (KeyValuePair<IBotHazardIsolationTarget, HazardIsolationMemoryRecord> pair in hazardIsolationRecords)
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

        public bool TryGetNearestRecentBreakable(Vector3 fromPosition, float maxDistance, out IBotBreakableTarget target)
        {
            CleanupExpiredBreakableRecords();

            target = null;
            float bestDistanceSq = float.PositiveInfinity;
            float maxDistanceSq = Mathf.Max(0.05f, maxDistance) * Mathf.Max(0.05f, maxDistance);

            foreach (KeyValuePair<IBotBreakableTarget, BreakableMemoryRecord> pair in breakableRecords)
            {
                IBotBreakableTarget candidate = pair.Value?.Target;
                if (IsUnityObjectDestroyed(candidate) || candidate.IsBroken || !candidate.CanBeClearedByBot)
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
            if (IsUnityObjectDestroyed(target))
            {
                return;
            }

            lastFollowTarget = target;
            lastFollowTargetTag = string.IsNullOrWhiteSpace(targetTag) ? string.Empty : targetTag;
            lastFollowTargetSeenTime = Time.time;
        }

        public bool TryGetRecentFollowTarget(string targetTag, out Transform target)
        {
            target = null;
            if (IsUnityObjectDestroyed(lastFollowTarget) || !lastFollowTarget.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(targetTag) &&
                !string.Equals(lastFollowTargetTag, targetTag, System.StringComparison.Ordinal))
            {
                return false;
            }

            if (IsExpired(lastFollowTargetSeenTime, Mathf.Max(0.1f, followTargetMemorySeconds)))
            {
                return false;
            }

            target = lastFollowTarget;
            return true;
        }

        private void CleanupExpiredRescueRecords()
        {
            CleanupExpiredRecords(rescueRecords, Mathf.Max(0.1f, rescueMemorySeconds), static record =>
                record == null || IsUnityObjectDestroyed(record.Target) || !record.Target.NeedsRescue, static record => record.LastSeenTime);
        }

        private void CleanupExpiredFireRecords()
        {
            CleanupExpiredRecords(fireRecords, Mathf.Max(0.1f, fireMemorySeconds), static record =>
                record == null || IsUnityObjectDestroyed(record.Target) || !record.Target.IsBurning, static record => record.LastSeenTime);
        }

        private void CleanupExpiredBreakableRecords()
        {
            CleanupExpiredRecords(breakableRecords, Mathf.Max(0.1f, breakableMemorySeconds), static record =>
                record == null || IsUnityObjectDestroyed(record.Target) || record.Target.IsBroken || !record.Target.CanBeClearedByBot, static record => record.LastSeenTime);
        }

        private void CleanupExpiredExtinguisherRecords()
        {
            CleanupExpiredRecords(extinguisherRecords, Mathf.Max(0.1f, extinguisherMemorySeconds), static record =>
                record == null || IsUnityObjectDestroyed(record.Target) || record.Target.IsHeld || !record.Target.HasUsableCharge, static record => record.LastSeenTime);
        }

        private void CleanupExpiredBreakToolRecords()
        {
            CleanupExpiredRecords(breakToolRecords, Mathf.Max(0.1f, breakToolMemorySeconds), static record =>
                record == null || IsUnityObjectDestroyed(record.Target) || record.Target.IsHeld, static record => record.LastSeenTime);
        }

        private void CleanupExpiredHazardIsolationRecords()
        {
            CleanupExpiredRecords(hazardIsolationRecords, Mathf.Max(0.1f, hazardIsolationMemorySeconds), static record =>
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
