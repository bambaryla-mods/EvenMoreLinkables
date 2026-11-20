using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace EvenMoreLinkables
{
    public class RequiredLinkablesToCraft : DefModExtension
    {
        public List<ThingDef> requiredLinkables;

        public static bool IsMissingRequiredLinkable(ThingDef producedThing, Thing worktable)
        {
            var extension = producedThing?.GetModExtension<RequiredLinkablesToCraft>();
            if (extension == null || !(worktable is Building_WorkTable))
            {
                return false;
            }
            var comp = worktable.TryGetComp<CompAffectedByFacilities>();
            return comp != null && !comp.LinkedFacilitiesListForReading.Any(x => extension.requiredLinkables.Contains(x.def));
        }
    }

    [HarmonyPatch(typeof(RecipeDef), "AvailableOnNow")]
    public static class RecipeDef_AvailableOnNow_Patch
    {
        public static void Postfix(RecipeDef __instance, Thing thing, ref bool __result)
        {
            if (__result && RequiredLinkablesToCraft.IsMissingRequiredLinkable(__instance.ProducedThingDef, thing))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Bill_Production), "ShouldDoNow")]
    public static class Bill_Production_ShouldDoNow_Patch
    {
        public static void Postfix(Bill_Production __instance, ref bool __result)
        {
            if (__result && RequiredLinkablesToCraft.IsMissingRequiredLinkable(__instance.recipe.ProducedThingDef, __instance.billStack.billGiver as Thing))
            {
                __result = false;
            }
        }
    }
}
