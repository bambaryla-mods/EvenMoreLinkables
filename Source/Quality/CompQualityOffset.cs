using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EvenMoreLinkables
{
    public class CompProperties_QualityOffset : CompProperties
    {
        public int qualityOffset;

        public CompProperties_QualityOffset()
        {
            compClass = typeof(CompQualityOffset);
        }
    }

    public class CompQualityOffset : ThingComp
    {
        public CompProperties_QualityOffset Props => props as CompProperties_QualityOffset;
    }

    [HarmonyPatch(typeof(QualityUtility), "GenerateQualityCreatedByPawn", [typeof(Pawn), typeof(SkillDef), typeof(bool)])]
    public static class QualityUtility_GenerateQualityCreatedByPawn_Patch
    {
        public static void Postfix(ref QualityCategory __result, Pawn pawn)
        {
            if (pawn.CurJob?.bill?.billStack?.billGiver is Building_WorkTable worktable)
            {
                var comp = worktable.GetComp<CompAffectedByFacilities>();
                if (comp != null)
                {
                    foreach (var link in comp.LinkedFacilitiesListForReading)
                    {
                        var props = link.TryGetComp<CompQualityOffset>();
                        if (props != null)
                        {
                            __result = (QualityCategory)((int)__result + props.Props.qualityOffset);
                            __result = (QualityCategory)Mathf.Clamp((int)__result, (int)QualityCategory.Awful, (int)QualityCategory.Legendary);
                            return;
                        }
                    }
                }
            }
        }
    }
}
