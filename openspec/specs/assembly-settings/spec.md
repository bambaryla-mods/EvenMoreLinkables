# Assembly: EvenMoreLinkables.Settings.dll

## Purpose
Optional Vanilla Expanded Framework (VEF) integration that exposes per-building tuning in the VEF mod settings page.

## Build & Deployment
- Source: `Source/Settings/EvenMoreLinkables.Settings.csproj`, `net48`, namespace `EvenMoreLinkables.Settings`.
- Output: `1.7/VEF/Assemblies/EvenMoreLinkables.Settings.dll`.
- Loaded only on 1.7 and only when `oskarpotocki.vanillafactionsexpanded.core` is active (`LoadFolders.xml`, `IfModActive`).
- References: `0ModSettingsFramework.dll` (from VEF 1.7; path overridable with `-p:VEFPath=`), `EvenMoreLinkables.dll` (1.7), `Krafs.Rimworld.Ref` 1.6.4871.

## Requirements

### Requirement: Building settings patch operation
`PatchOperationBuildingSettings : PatchOperationWorker` SHALL let the player edit, for every building added by this mod:
- facility stat offsets (`WorkTableWorkSpeedFactor`, `ResearchSpeedFactor`, `PowerOutputFactor`; max 1000%),
- quality offset (max 6),
- build cost for ComponentIndustrial, ComponentSpacer, Steel, Plasteel (max 99999),
- power consumption (1-10000).

#### Scenario: Only overrides persisted
- WHEN a value equals the XML default
- THEN it is not stored, so later XML default changes still apply

### Requirement: Settings lifecycle
The worker SHALL implement `ApplySettings`, `DoSettings`, `ExposeData`, `CopyFrom`, and `Reset`. Values are keyed `defName|stat|<stat>`, `defName|cost|<ingredient>`, `defName|quality`, `defName|power`.

### Requirement: Private field access
`basePowerConsumption` of `CompProperties_Power` SHALL be accessed via reflection since it is not public in the reference assemblies.
