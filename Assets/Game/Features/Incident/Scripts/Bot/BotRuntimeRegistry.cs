using System.Collections.Generic;

namespace TrueJourney.BotBehavior
{
    public static class BotRuntimeRegistry
    {
        private static readonly HashSet<IBotExtinguisherItem> ExtinguisherItems = new HashSet<IBotExtinguisherItem>();
        private static readonly HashSet<IFireTarget> FireTargets = new HashSet<IFireTarget>();
        private static readonly HashSet<IFireGroupTarget> FireGroupTargets = new HashSet<IFireGroupTarget>();
        private static readonly HashSet<IBotBreakTool> BreakTools = new HashSet<IBotBreakTool>();
        private static readonly HashSet<IBotBreakableTarget> BreakableTargets = new HashSet<IBotBreakableTarget>();
        private static readonly HashSet<IBotPryTarget> PryTargets = new HashSet<IBotPryTarget>();
        private static readonly HashSet<IRescuableTarget> RescuableTargets = new HashSet<IRescuableTarget>();
        private static readonly HashSet<ISafeZoneTarget> SafeZoneTargets = new HashSet<ISafeZoneTarget>();
        private static readonly HashSet<IBotHazardIsolationTarget> HazardIsolationTargets = new HashSet<IBotHazardIsolationTarget>();
        private static readonly HashSet<global::BotCommandAgent> CommandAgents = new HashSet<global::BotCommandAgent>();
        private static readonly HashSet<IThermalSignatureSource> ThermalSignatureSources = new HashSet<IThermalSignatureSource>();
        private static readonly global::BotReservationSystem ReservationState = new global::BotReservationSystem();
        private static readonly BotSharedIncidentBlackboard SharedIncidentState = new BotSharedIncidentBlackboard();

        public static IEnumerable<IBotExtinguisherItem> ActiveExtinguisherItems => ExtinguisherItems;
        public static IEnumerable<IFireTarget> ActiveFireTargets => FireTargets;
        public static IEnumerable<IFireGroupTarget> ActiveFireGroups => FireGroupTargets;
        public static IEnumerable<IBotBreakTool> ActiveBreakTools => BreakTools;
        public static IEnumerable<IBotBreakableTarget> ActiveBreakableTargets => BreakableTargets;
        public static IEnumerable<IBotPryTarget> ActivePryTargets => PryTargets;
        public static IEnumerable<IRescuableTarget> ActiveRescuableTargets => RescuableTargets;
        public static IEnumerable<ISafeZoneTarget> ActiveSafeZones => SafeZoneTargets;
        public static IEnumerable<IBotHazardIsolationTarget> ActiveHazardIsolationTargets => HazardIsolationTargets;
        public static IEnumerable<global::BotCommandAgent> ActiveCommandAgents => CommandAgents;
        public static IEnumerable<IThermalSignatureSource> ActiveThermalSignatureSources => ThermalSignatureSources;
        public static global::BotReservationSystem Reservations => ReservationState;
        public static BotSharedIncidentBlackboard SharedIncidentBlackboard => SharedIncidentState;

        public static void RegisterCommandAgent(global::BotCommandAgent agent)
        {
            if (agent != null)
            {
                CommandAgents.Add(agent);
            }
        }

        public static void UnregisterCommandAgent(global::BotCommandAgent agent)
        {
            if (agent != null)
            {
                CommandAgents.Remove(agent);
            }
        }

        public static void RegisterExtinguisherItem(IBotExtinguisherItem item)
        {
            if (item != null)
            {
                ExtinguisherItems.Add(item);
            }
        }

        public static void UnregisterExtinguisherItem(IBotExtinguisherItem item)
        {
            if (item != null)
            {
                ExtinguisherItems.Remove(item);
            }
        }

        public static void RegisterFireTarget(IFireTarget target)
        {
            if (target != null)
            {
                FireTargets.Add(target);
            }
        }

        public static void UnregisterFireTarget(IFireTarget target)
        {
            if (target != null)
            {
                FireTargets.Remove(target);
            }
        }

        public static void RegisterFireGroup(IFireGroupTarget group)
        {
            if (group != null)
            {
                FireGroupTargets.Add(group);
            }
        }

        public static void UnregisterFireGroup(IFireGroupTarget group)
        {
            if (group != null)
            {
                FireGroupTargets.Remove(group);
            }
        }

        public static void RegisterBreakTool(IBotBreakTool tool)
        {
            if (tool != null)
            {
                BreakTools.Add(tool);
            }
        }

        public static void UnregisterBreakTool(IBotBreakTool tool)
        {
            if (tool != null)
            {
                BreakTools.Remove(tool);
            }
        }

        public static void RegisterBreakableTarget(IBotBreakableTarget target)
        {
            if (target != null)
            {
                BreakableTargets.Add(target);
            }
        }

        public static void RegisterPryTarget(IBotPryTarget target)
        {
            if (target != null)
            {
                PryTargets.Add(target);
            }
        }

        public static void UnregisterBreakableTarget(IBotBreakableTarget target)
        {
            if (target != null)
            {
                BreakableTargets.Remove(target);
            }
        }

        public static void UnregisterPryTarget(IBotPryTarget target)
        {
            if (target != null)
            {
                PryTargets.Remove(target);
            }
        }

        public static void RegisterRescuableTarget(IRescuableTarget target)
        {
            if (target != null)
            {
                RescuableTargets.Add(target);
            }
        }

        public static void UnregisterRescuableTarget(IRescuableTarget target)
        {
            if (target != null)
            {
                RescuableTargets.Remove(target);
            }
        }

        public static void RegisterSafeZone(ISafeZoneTarget target)
        {
            if (target != null)
            {
                SafeZoneTargets.Add(target);
            }
        }

        public static void UnregisterSafeZone(ISafeZoneTarget target)
        {
            if (target != null)
            {
                SafeZoneTargets.Remove(target);
            }
        }

        public static void RegisterHazardIsolationTarget(IBotHazardIsolationTarget target)
        {
            if (target != null)
            {
                HazardIsolationTargets.Add(target);
            }
        }

        public static void UnregisterHazardIsolationTarget(IBotHazardIsolationTarget target)
        {
            if (target != null)
            {
                HazardIsolationTargets.Remove(target);
            }
        }

        public static void RegisterThermalSignatureSource(IThermalSignatureSource source)
        {
            if (source != null)
            {
                ThermalSignatureSources.Add(source);
            }
        }

        public static void UnregisterThermalSignatureSource(IThermalSignatureSource source)
        {
            if (source != null)
            {
                ThermalSignatureSources.Remove(source);
            }
        }
    }
}
