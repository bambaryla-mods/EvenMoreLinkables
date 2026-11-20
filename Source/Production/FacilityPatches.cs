using HarmonyLib;
using RimWorld;

namespace Hobbes.MoreLinkablesProduction
{
    // A work helper in WORK mode stops acting as a facility for nearby workbenches.
    [HarmonyPatch(typeof(CompFacility), nameof(CompFacility.CanBeActive), MethodType.Getter)]
    public static class CompFacility_CanBeActive_Patch
    {
        public static void Postfix(CompFacility __instance, ref bool __result)
        {
            CompWorkHelper comp = __instance.parent.GetComp<CompWorkHelper>();
            if (comp != null && comp.state == CompWorkHelper.State.WORK)
            {
                __result = false;
            }
        }
    }
}
