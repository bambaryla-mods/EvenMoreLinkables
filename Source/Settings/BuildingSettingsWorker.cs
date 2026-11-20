using System;
using System.Collections.Generic;
using System.Linq;
using ModSettingsFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace EvenMoreLinkables.Settings
{
    // Lets the player tune every building this mod adds (facility stat offsets, quality offset and build cost)
    // through the Vanilla Expanded Framework mod settings page. Only values that differ from the XML defaults
    // are stored, so changing a default in the defs still reaches players who never touched that value.
    public class PatchOperationBuildingSettings : PatchOperationWorker
    {
        private const float MaxStatOffset = 10f; // 1000%
        private const int MaxQualityOffset = 6;
        private const int MaxCost = 99999;
        private const int MinPower = 1;
        private const int MaxPower = 10000;
        private const float RowHeight = 28f;
        private const float CostBoxWidth = 90f;

        // Every building offers all of these, even when its XML cost list omits some of them.
        private static readonly string[] Ingredients =
        {
            "ComponentIndustrial",
            "ComponentSpacer",
            "Steel",
            "Plasteel",
        };

        private static readonly Dictionary<string, string> StatLabels = new Dictionary<string, string>
        {
            ["WorkTableWorkSpeedFactor"] = "Work speed improve",
            ["ResearchSpeedFactor"] = "Research speed improve",
            ["PowerOutputFactor"] = "Power output improve",
        };

        // Not public in the reference assemblies.
        private static readonly System.Reflection.FieldInfo PowerConsumptionField =
            typeof(CompProperties_Power).GetField("basePowerConsumption", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        private static Dictionary<string, float> defaults;
        private static List<ThingDef> buildings;

        private Dictionary<string, float> values = new Dictionary<string, float>();
        private readonly Dictionary<string, string> numberBuffers = new Dictionary<string, string>();
        private int rowIndex;

        private static string StatKey(ThingDef def, StatDef stat) => def.defName + "|stat|" + stat.defName;
        private static string CostKey(ThingDef def, string ingredient) => def.defName + "|cost|" + ingredient;
        private static string QualityKey(ThingDef def) => def.defName + "|quality";
        private static string PowerKey(ThingDef def) => def.defName + "|power";

        private float Get(string key)
        {
            return values.TryGetValue(key, out float value) ? value : defaults[key];
        }

        private void Set(string key, float value)
        {
            if (Mathf.Approximately(value, defaults[key]))
            {
                values.Remove(key);
            }
            else
            {
                values[key] = value;
            }
        }

        private void EnsureDefaults()
        {
            if (defaults != null)
            {
                return;
            }
            defaults = new Dictionary<string, float>();
            buildings = new List<ThingDef>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.modContentPack != modContentPack || def.comps == null || !def.comps.Any(c => c is CompProperties_Facility))
                {
                    continue;
                }
                buildings.Add(def);
                foreach (CompProperties comp in def.comps)
                {
                    if (comp is CompProperties_Facility facility && facility.statOffsets != null)
                    {
                        foreach (StatModifier modifier in facility.statOffsets)
                        {
                            defaults[StatKey(def, modifier.stat)] = modifier.value;
                        }
                    }
                    else if (comp is CompProperties_QualityOffset quality)
                    {
                        defaults[QualityKey(def)] = quality.qualityOffset;
                    }
                    else if (comp is CompProperties_Power power && power.PowerConsumption > 0f)
                    {
                        defaults[PowerKey(def)] = power.PowerConsumption;
                    }
                }
                foreach (string ingredient in Ingredients)
                {
                    ThingDefCountClass existing = def.costList?.FirstOrDefault(c => c.thingDef.defName == ingredient);
                    defaults[CostKey(def, ingredient)] = existing?.count ?? 0;
                }
            }
            buildings.SortBy(d => d.label);
        }

        public override void ApplySettings()
        {
            // The framework replaces its container dictionary when the saved settings load, after this patch
            // cached its container. Re-fetching registers this mod's settings page in the live dictionary.
            SettingsContainer = ModSettingsFrameworkSettings.GetModSettingsContainer(modContentPack);
            base.ApplySettings();
            EnsureDefaults();
            foreach (ThingDef def in buildings)
            {
                foreach (CompProperties comp in def.comps)
                {
                    if (comp is CompProperties_Facility facility && facility.statOffsets != null)
                    {
                        foreach (StatModifier modifier in facility.statOffsets)
                        {
                            modifier.value = Get(StatKey(def, modifier.stat));
                        }
                    }
                    else if (comp is CompProperties_QualityOffset quality)
                    {
                        quality.qualityOffset = (int)Get(QualityKey(def));
                    }
                    else if (comp is CompProperties_Power power && defaults.ContainsKey(PowerKey(def)))
                    {
                        PowerConsumptionField.SetValue(power, Get(PowerKey(def)));
                    }
                }
                ApplyCost(def);
            }
        }

        private void ApplyCost(ThingDef def)
        {
            def.costList ??= new List<ThingDefCountClass>();
            foreach (string ingredient in Ingredients)
            {
                int count = (int)Get(CostKey(def, ingredient));
                ThingDefCountClass existing = def.costList.FirstOrDefault(c => c.thingDef.defName == ingredient);
                if (count <= 0)
                {
                    if (existing != null)
                    {
                        def.costList.Remove(existing);
                    }
                }
                else if (existing != null)
                {
                    existing.count = count;
                }
                else
                {
                    def.costList.Add(new ThingDefCountClass(DefDatabase<ThingDef>.GetNamed(ingredient), count));
                }
            }
        }

        public override void DoSettings(ModSettingsContainer container, Listing_Standard list)
        {
            EnsureDefaults();
            rowIndex = 0;
            // Leaves room for the framework's Reset button and warning at the top of the window.
            list.Gap(40f);
            scrollHeight += 40f;
            foreach (ThingDef def in buildings)
            {
                DoBuilding(list, def);
            }
            // The container only syncs workers drawn inside a category, so push the edits to the saved instance here.
            CopyValues();
        }

        private void DoBuilding(Listing_Standard list, ThingDef def)
        {
            Rect header = list.GetRect(32f);
            Widgets.ThingIcon(new Rect(header.x, header.y, 32f, 32f), def);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(header.x + 40f, header.y, header.width - 40f, header.height), def.LabelCap);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(header, def.description);
            scrollHeight += header.height;

            string summary = Summary(def);
            if (!summary.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                GUI.color = Color.grey;
                Rect summaryRect = list.GetRect(Text.LineHeight);
                Widgets.Label(summaryRect, summary);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                scrollHeight += summaryRect.height;
            }
            list.Gap(4f);
            scrollHeight += 4f;

            foreach (CompProperties comp in def.comps)
            {
                if (comp is CompProperties_Facility facility && facility.statOffsets != null)
                {
                    foreach (StatModifier modifier in facility.statOffsets)
                    {
                        DoStatSlider(list, def, modifier.stat);
                    }
                }
                else if (comp is CompProperties_QualityOffset)
                {
                    DoQualitySlider(list, def);
                }
            }

            if (defaults.ContainsKey(PowerKey(def)))
            {
                DoSubheading(list, "Power");
                DoNumberRow(list, PowerKey(def), null, "Power consumption (W)", "Default: " + (int)defaults[PowerKey(def)] + " W", MinPower, MaxPower);
            }

            DoSubheading(list, "Build cost");
            foreach (string ingredient in Ingredients)
            {
                ThingDef thing = DefDatabase<ThingDef>.GetNamed(ingredient);
                DoNumberRow(list, CostKey(def, ingredient), thing, thing.LabelCap,
                    "Default: " + (int)defaults[CostKey(def, ingredient)] + ". Set to 0 to leave this ingredient out of the cost.", 0, MaxCost);
            }

            list.GapLine(12f);
            scrollHeight += 12f;
        }

        private static string Summary(ThingDef def)
        {
            var parts = new List<string>();
            CompProperties_Facility facility = def.GetCompProperties<CompProperties_Facility>();
            if (facility != null)
            {
                bool powerLink = facility.statOffsets != null && facility.statOffsets.Any(m => m.stat.defName == "PowerOutputFactor");
                parts.Add("max " + facility.maxSimultaneous + (powerLink ? " per power source" : " per workbench"));
                parts.Add(facility.mustBePlacedAdjacent ? "must be adjacent" : "within " + facility.maxDistance.ToString("0.#") + " tiles");
            }
            return string.Join(" · ", parts);
        }

        private void DoStatSlider(Listing_Standard list, ThingDef def, StatDef stat)
        {
            string key = StatKey(def, stat);
            float value = Get(key);
            string label = StatLabels.TryGetValue(stat.defName, out string known) ? known : stat.LabelCap.ToString();
            DoSlider(list, label, ref value, Mathf.RoundToInt(value * 100f) + "%", 0f, MaxStatOffset,
                "Default: " + Mathf.RoundToInt(defaults[key] * 100f) + "%");
            Set(key, Mathf.Clamp(value, 0f, MaxStatOffset));
        }

        private void DoQualitySlider(Listing_Standard list, ThingDef def)
        {
            string key = QualityKey(def);
            int value = (int)Get(key);
            DoSlider(list, "Quality improve", ref value, "+" + value, 0, MaxQualityOffset,
                "Quality levels added to items crafted at the linked workbench. Default: +" + (int)defaults[key]);
            Set(key, Mathf.Clamp(value, 0, MaxQualityOffset));
        }

        private void DoSubheading(Listing_Standard list, string text)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = Color.grey;
            Rect rect = list.GetRect(Text.LineHeight);
            Widgets.Label(rect, text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            scrollHeight += rect.height;
        }

        private void DoNumberRow(Listing_Standard list, string key, ThingDef icon, string label, string tooltip, int min, int max)
        {
            Rect row = list.GetRect(RowHeight);
            if (rowIndex++ % 2 == 0)
            {
                Widgets.DrawLightHighlight(row);
            }
            float labelX = row.x + 4f;
            if (icon != null)
            {
                Widgets.ThingIcon(new Rect(row.x + 4f, row.y + 2f, row.height - 4f, row.height - 4f), icon);
                labelX = row.x + row.height + 4f;
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(labelX, row.y, row.xMax - CostBoxWidth - labelX - 4f, row.height), label);
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(row, tooltip);

            int count = (int)Get(key);
            numberBuffers.TryGetValue(key, out string buffer);
            Widgets.TextFieldNumeric(new Rect(row.xMax - CostBoxWidth, row.y + 1f, CostBoxWidth, row.height - 2f), ref count, ref buffer, min, max);
            numberBuffers[key] = buffer;
            Set(key, count);
            scrollHeight += row.height;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref values, "values", LookMode.Value, LookMode.Value);
            values ??= new Dictionary<string, float>();
        }

        public override void CopyFrom(PatchOperationWorker savedWorker)
        {
            if (savedWorker is PatchOperationBuildingSettings saved && saved != this)
            {
                values = saved.values != null ? new Dictionary<string, float>(saved.values) : new Dictionary<string, float>();
            }
        }

        public override void Reset()
        {
            values.Clear();
            numberBuffers.Clear();
        }
    }
}
