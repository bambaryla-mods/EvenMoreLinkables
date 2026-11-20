# Assembly: FixPowerMeterOverflow.dll (third-party, bundled)

## Purpose
Keeps the power meter drawn on generators from overflowing when facility bonuses raise output above nominal. It was written outside this repo, so there is no source under `Source/`.

## Provenance
- Package id `com.deadmano.FixPowerMeterOverflow`, namespace `FixPowerMeterOverflow`; uses Harmony (`0Harmony`).
- Shipped binaries: `1.6/Assemblies/FixPowerMeterOverflow.dll`, `1.7/Assemblies/FixPowerMeterOverflow.dll`.
- It replaces `FixSolarPanelMeterOverflow.dll`, which the 1.4 and 1.5 folders used to ship.

## Requirements

### Requirement: Transpile PostDraw meter fill
`ApplyPatches` SHALL transpile (`PatchPostDraw` / `PatchPostDrawFillPercent`, `FillPercentTranspiler`) `PostDraw` of these classes so the fill percent is clamped:
- `RimWorld.CompPowerPlantSolar`
- `RimWorld.CompPowerPlantWind`
- `SmallSolarPanels.CompPowerPlantSolarSmall`
- `VanillaPowerExpanded.CompPowerAdvancedSolar`
- `VanillaPowerExpanded.CompPowerAdvancedWind`

#### Scenario: Class absent
- WHEN a target mod's class is not installed
- THEN a "Failed to find internal class ... for patching" message is logged and the rest still patch

## Relationship to core
The `PowerOutputFactor` requirement in `assembly-core` is what pushes output past nominal, so the meter needs this fix.
