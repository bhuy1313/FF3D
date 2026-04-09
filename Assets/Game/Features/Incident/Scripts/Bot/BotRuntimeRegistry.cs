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
        private static readonly HashSet<IRescuableTarget> RescuableTargets = new HashSet<IRescuableTarget>();
        private static readonly HashSet<ISafeZoneTarget> SafeZoneTargets = new HashSet<ISafeZoneTarget>();
        private static readonly HashSet<global::BotCommandAgent> CommandAgents = new HashSet<global::BotCommandAgent>();
        private static readonly HashSet<IThermalSignatureSource> ThermalSignatureSources = new HashSet<IThermalSignatureSource>();

        public static IEnumerable<IBotExtinguisherItem> ActiveExtinguisherItems => ExtinguisherItems;
        public static IEnumerable<IFireTarget> ActiveFireTargets => FireTargets;
        public static IEnumerable<IFireGroupTarget> ActiveFireGroups => FireGroupTargets;
        public static IEnumerable<IBotBreakTool> ActiveBreakTools => BreakTools;
        public static IEnumerable<IBotBreakableTarget> ActiveBreakableTargets => BreakableTargets;
        public static IEnumerable<IRescuableTarget> ActiveRescuableTargets => RescuableTargets;
        public static IEnumerable<ISafeZoneTarget> ActiveSafeZones => SafeZoneTargets;
        public static IEnumerable<global::BotCommandAgent> ActiveCommandAgents => CommandAgents;
        public static IEnumerable<IThermalSignatureSource> ActiveThermalSignatureSources => ThermalSignatureSources;

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

        public static void UnregisterBreakableTarget(IBotBreakableTarget target)
        {
            if (target != null)
            {
                BreakableTargets.Remove(target);
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
