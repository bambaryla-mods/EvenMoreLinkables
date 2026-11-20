---
summary: Transformers, cooling towers and other equipment that raise the output of any generator, from a wind turbine to a modded reactor.
card: HobbesLink_CoolingModule, HobbesLink_Transformer, HobbesLink_Alternator
---
Power linkables raise how much electricity a generator makes. They link to wood-fired and chemfuel generators, solar panels, wind turbines, geothermal generators and the rest of vanilla's generators. Generators from other mods take them too, as long as they are built the way vanilla generators are. The gravship's grav engine is the one exception.

They don't use power themselves.

## How the bonus adds up

Each linked building adds its percentage, and the generator's output is multiplied by the total. A generator with two cooling modules ({{ value("HobbesLink_CoolingModule", "power output", "pct") }} each) and one alternator ({{ value("HobbesLink_Alternator", "power output", "pct") }}) gets +100%, so it makes twice its normal power.

The bonus follows what the generator is doing at the moment. A solar panel still makes nothing at night, and a wind turbine still depends on the wind. The power meter drawn on solar panels and wind turbines would overflow its frame when they are boosted; the bundled Fix Power Meter Overflow keeps it inside.

## Buildings

{{ buildings("power") }}

"Touching" means the building has to stand right next to the generator. The others work from a distance, measured from the generator.
