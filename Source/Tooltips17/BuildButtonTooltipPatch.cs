using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EvenMoreLinkables.UI
{
    [StaticConstructorOnStartup]
    public static class TooltipsStartup
    {
        static TooltipsStartup()
        {
            new Harmony("EvenMoreLinkables.Tooltips").PatchAll(typeof(TooltipsStartup).Assembly);
        }
    }

    // Vanilla turns tooltips off for every build button because the Architect shows the description in its info box.
    // The add-on buttons of a selected bench or generator are the same Designator_Build objects without that box,
    // so this brings the tooltip back for facilities, with how many are already linked to the selection.
    [HarmonyPatch(typeof(Command), "GizmoOnGUIInt")]
    public static class Command_GizmoOnGUIInt_Patch
    {
        public static void Postfix(Command __instance, Rect butRect)
        {
            if (!(__instance is Designator_Build designator))
            {
                return;
            }

            if (Find.MainTabsRoot.OpenTab == MainButtonDefOf.Architect)
            {
                return;
            }

            ThingDef def = designator.PlacingDef as ThingDef;
            CompProperties_Facility props = def?.GetCompProperties<CompProperties_Facility>();
            if (props == null)
            {
                return;
            }

            TooltipHandler.TipRegion(butRect, BuildTip(def, props));
        }

        private static string BuildTip(ThingDef def, CompProperties_Facility props)
        {
            var sb = new StringBuilder();
            sb.AppendLine(def.LabelCap.Colorize(ColoredText.TipSectionTitleColor));
            if (!def.description.NullOrEmpty())
            {
                sb.AppendLine().AppendLine(def.description);
            }

            if (props.statOffsets != null && props.statOffsets.Count > 0)
            {
                sb.AppendLine();
                foreach (StatModifier offset in props.statOffsets)
                {
                    sb.AppendLine($"{offset.stat.LabelCap}: {offset.stat.Worker.ValueToString(offset.value, false, ToStringNumberSense.Offset)}");
                }
            }

            sb.AppendLine();
            if (props.mustBePlacedAdjacent)
            {
                sb.AppendLine("Must be placed adjacent");
            }
            else
            {
                sb.AppendLine($"Max distance: {props.maxDistance:0.#}");
            }

            string limit = props.maxSimultaneous.ToString();
            if (Find.Selector.SingleSelectedThing is ThingWithComps selected)
            {
                CompAffectedByFacilities affected = selected.GetComp<CompAffectedByFacilities>();
                if (affected != null)
                {
                    int linked = affected.LinkedFacilitiesListForReading.Count(f => f.def == def);
                    limit = $"{linked} / {props.maxSimultaneous}";
                }
            }

            sb.Append($"Linked: {limit}");
            return sb.ToString();
        }
    }
}
