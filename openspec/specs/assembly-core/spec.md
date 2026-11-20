# Assembly: EvenMoreLinkables.dll

## Purpose
Core gameplay logic for the mod. Registers Harmony patches and supplies the custom `ThingComp` / `DefModExtension` types referenced from `Defs/`.

## Build & Deployment
- Source: `Source/EvenMoreLinkables.csproj` (assembly name `EvenMoreLinkables`, .NET Framework 4.8, C# latest).
- Output: `1.6/Assemblies/EvenMoreLinkables.dll` and `1.7/Assemblies/EvenMoreLinkables.dll` (loaded via `LoadFolders.xml`).
- References: `Assembly-CSharp`, `UnityEngine.CoreModule`, `Krafs.Rimworld.Ref` 1.6.4871, `Krafs.Publicizer` 2.*, `Lib.Harmony` 2.* (compile only; the Harmony mod provides it at runtime).

## Requirements

### Requirement: Harmony bootstrap
The assembly SHALL apply all `[HarmonyPatch]` classes at startup via the `EvenMoreLinkablesMod` constructor using Harmony id `EvenMoreLinkablesMod`.

#### Scenario: Mod loads
- WHEN RimWorld instantiates `EvenMoreLinkablesMod(ModContentPack)`
- THEN `new Harmony("EvenMoreLinkablesMod").PatchAll()` is invoked for this assembly

### Requirement: Power output factor
Power plants with `CompAffectedByFacilities` SHALL have their `PowerOutput` multiplied by the `PowerOutputFactor` stat (`Hobbes.MoreLinkablesPower.LinkedStatDefOf.PowerOutputFactor`).

- Patches: `CompPowerPlant.UpdateDesiredPowerOutput` (postfix); `CompAffectedByFacilities.Notify_NewLink`, `Notify_LinkRemoved`, `Notify_FacilityDespawned`, `Notify_ThingChanged` (postfix, marks cache dirty).
- The factor is cached per Thing (`ConditionalWeakTable`) and recomputed when dirty or every 600 ticks (staggered by thing ID).

#### Scenario: Plant with linked facility
- GIVEN a power plant with a linked facility offering `PowerOutputFactor`
- WHEN `UpdateDesiredPowerOutput` runs and `PowerOutput != 0`
- THEN `PowerOutput` is multiplied by the cached factor

#### Scenario: Plant without facility comp
- WHEN the plant has no `CompAffectedByFacilities`
- THEN `PowerOutput` is unchanged

### Requirement: Required linkables to craft
A `ThingDef` carrying `RequiredLinkablesToCraft` (`requiredLinkables: List<ThingDef>`) SHALL only be craftable at a `Building_WorkTable` that has at least one of those facilities linked.

- Patches: `RecipeDef.AvailableOnNow` (postfix), `Bill_Production.ShouldDoNow` (postfix) force the result to `false` when no required facility is linked.

#### Scenario: Missing facility
- GIVEN a recipe whose product requires facility X and the bench has no linked X
- THEN the recipe is unavailable and existing bills do not run

### Requirement: Quality offset
Facilities with `CompQualityOffset` (`CompProperties_QualityOffset.qualityOffset`: int) SHALL shift the quality of items crafted at a linked bench.

- Patch: `QualityUtility.GenerateQualityCreatedByPawn(Pawn, SkillDef, bool)` (postfix).
- Only the first linked facility with the comp applies; result is clamped to Awful..Legendary.

#### Scenario: Offset +1
- GIVEN a pawn working a bill at a bench with a linked facility of offset +1
- THEN a Normal result becomes Good

### Requirement: Work helper
`CompWorkHelper` (`CompProperties_WorkHelper`: `radius`=5, `tickInterval`=100, `reductionAmt`=100, `workIconPath`, `boostIconPath`; these are the code defaults, and the robot assistant's def sets `tickInterval` to 2000) SHALL operate in two persisted modes (`WORK`, `BOOST`) toggled by a gizmo.

- In `WORK` mode and powered, every `tickInterval` ticks it reduces `workLeft` of one random `UnfinishedThing` in radius by `reductionAmt` seconds (x60 ticks) and shows a mote.
- In `WORK` mode it SHALL NOT act as a facility: patch `CompFacility.CanBeActive` (getter, postfix) forces `false`.
- Requires `CompPowerTrader` on the parent.

#### Scenario: Mode switch
- WHEN the player clicks the gizmo in WORK mode
- THEN state becomes BOOST and the facility becomes active for linked benches

### Requirement: Second layer rendering
`CompSecondLayer` (`CompProperties_SecondLayer`) SHALL draw an additional graphic layer in `PostDraw`.

## Public Surface (XML-referenced types)
| Type | Namespace | Kind |
|---|---|---|
| `CompProperties_QualityOffset` / `CompQualityOffset` | EvenMoreLinkables | Comp |
| `RequiredLinkablesToCraft` | EvenMoreLinkables | DefModExtension |
| `CompProperties_WorkHelper` / `CompWorkHelper` | Hobbes.MoreLinkablesProduction | Comp |
| `CompProperties_SecondLayer` / `CompSecondLayer` | Hobbes.MoreLinkablesProduction | Comp |
| `LinkedStatDefOf` | Hobbes.MoreLinkablesPower | DefOf |

## Notes
- The mod no longer ships `MoreLinkablesAssembly.dll` or any 1.4/1.5 assemblies.
