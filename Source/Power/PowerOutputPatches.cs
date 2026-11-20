using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;

namespace Hobbes.MoreLinkablesPower
{
    [DefOf]
    public static class LinkedStatDefOf
    {
        public static StatDef PowerOutputFactor;

        static LinkedStatDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LinkedStatDefOf));
        }
    }

    // UpdateDesiredPowerOutput runs every tick for every power plant and resets PowerOutput each time,
    // so the multiplication has to be reapplied every tick, but the factor itself is cached and only
    // recalculated when links change. The periodic refresh covers changes that raise no link
    // notification, such as a linked facility losing power.
    public static class PowerOutputFactorCache
    {
        private const int SafetyRefreshInterval = 600;

        private class Entry
        {
            public bool dirty = true;
            public bool hasLinks;
            public float factor = 1f;
            public int nextRefreshTick;
        }

        private static readonly ConditionalWeakTable<Thing, Entry> entries = new ConditionalWeakTable<Thing, Entry>();

        public static void MarkDirty(Thing thing)
        {
            if (entries.TryGetValue(thing, out Entry entry))
            {
                entry.dirty = true;
            }
        }

        public static void ApplyTo(CompPowerPlant plant)
        {
            ThingWithComps thing = plant.parent;
            Entry entry = entries.GetOrCreateValue(thing);
            int now = Find.TickManager.TicksGame;
            if (entry.dirty || now >= entry.nextRefreshTick)
            {
                entry.dirty = false;
                // Offset by thing ID so plants don't all refresh on the same tick.
                entry.nextRefreshTick = now + SafetyRefreshInterval - (now + thing.thingIDNumber) % SafetyRefreshInterval;
                entry.hasLinks = thing.GetComp<CompAffectedByFacilities>() != null;
                entry.factor = entry.hasLinks ? LinkedStatDefOf.PowerOutputFactor.Worker.GetValue(thing) : 1f;
            }
            if (entry.hasLinks)
            {
                plant.PowerOutput *= entry.factor;
            }
        }
    }

    [HarmonyPatch(typeof(CompPowerPlant), nameof(CompPowerPlant.UpdateDesiredPowerOutput))]
    public static class CompPowerPlant_UpdateDesiredPowerOutput_Patch
    {
        public static void Postfix(CompPowerPlant __instance)
        {
            if (__instance.PowerOutput != 0.0f)
            {
                PowerOutputFactorCache.ApplyTo(__instance);
            }
        }
    }

    [HarmonyPatch]
    public static class CompAffectedByFacilities_LinksChanged_Patch
    {
        private static readonly string[] notifications =
        {
            nameof(CompAffectedByFacilities.Notify_NewLink),
            nameof(CompAffectedByFacilities.Notify_LinkRemoved),
            nameof(CompAffectedByFacilities.Notify_FacilityDespawned),
            nameof(CompAffectedByFacilities.Notify_ThingChanged),
        };

        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in notifications)
            {
                yield return AccessTools.Method(typeof(CompAffectedByFacilities), name);
            }
        }

        public static void Postfix(CompAffectedByFacilities __instance)
        {
            PowerOutputFactorCache.MarkDirty(__instance.parent);
        }
    }
}
