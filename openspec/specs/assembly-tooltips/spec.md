# Assembly: EvenMoreLinkables.Tooltips.dll

## Purpose
UI add-on for RimWorld 1.7 only. It restores tooltips on facility build buttons shown in a selected bench/generator's gizmo bar.

## Build & Deployment
- Source: `Source/Tooltips17/Tooltips17.csproj`, `net10.0`, assembly `EvenMoreLinkables.Tooltips`, namespace `EvenMoreLinkables.UI`.
- Output: `1.7/Assemblies/EvenMoreLinkables.Tooltips.dll`. Never loaded for 1.6 (`LoadFolders.xml`).
- References (not copied): `Assembly-CSharp` and `UnityStubs` from the 1.7 game directory; `Lib.Harmony` 2.4.2 (runtime excluded).

## Requirements

### Requirement: Self-patching on startup
A `[StaticConstructorOnStartup]` class `TooltipsStartup` SHALL patch the assembly with Harmony id `EvenMoreLinkables.Tooltips`.

### Requirement: Facility build-button tooltip
Postfix on `Command.GizmoOnGUIInt` SHALL register a tooltip for `Designator_Build` buttons whose `ThingDef` has `CompProperties_Facility`, except while the Architect main tab is open.

The tooltip SHALL show: label, description, stat offsets, placement rule (adjacent or max distance), and `Linked: n / max` when a single `CompAffectedByFacilities` thing is selected (otherwise just the max).

#### Scenario: Selected workbench
- GIVEN a selected workbench with 1 of 2 allowed facilities linked
- WHEN hovering the facility's build button
- THEN the tooltip ends with `Linked: 1 / 2`

#### Scenario: Architect tab open
- THEN no tooltip is added (vanilla info box is used)
