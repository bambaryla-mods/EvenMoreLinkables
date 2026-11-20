---
summary: Machines that make crafting benches work faster, a robot arm that finishes work on its own, and a laser engraver that raises quality.
card: HobbesLink_CNCMachine, HobbesLink_RobotAssistant, HobbesLink_Lathe
---
Workshop linkables go next to crafting benches: the machining table, fabrication bench, smithies, tailor benches, art bench, stonecutter's table, electric smelter and electric crematorium, plus benches from [many other mods](compatibility.html). Most of them need power, and their bonuses add up with the tool cabinet's.

A few of them also help research. The autoclave, centrifuge and incinerator link to research benches as well.

## Buildings

{{ buildings("workshop") }}

"Touching" means the building has to stand right next to the bench. The others work from a distance, measured from the bench.

## Robot assistant

The robot assistant has a button that switches it between two modes:

- In boost mode it is an ordinary linkable and adds {{ value("HobbesLink_RobotAssistant", "work speed", "pct") }} work speed to the bench it touches.
- In work mode it stops counting as a linkable. Instead, while it has power, it takes {{ value("HobbesLink_RobotAssistant", "reductionAmt") }} work off one unfinished item within {{ value("HobbesLink_RobotAssistant", "radius") }} tiles every {{ value("HobbesLink_RobotAssistant", "tickInterval", "seconds") }} seconds at normal speed. A number floats up showing how much work is left.

Work mode helps with long jobs that sit unfinished, such as a big sculpture or a suit of power armor.

## Laser engraver

A bench linked to a laser engraver makes everything {{ value("HobbesLink_LaserEngraver", "quality") }} quality level better than the crafter's roll: a normal item comes out good, an excellent one masterwork. It can't go above legendary.
