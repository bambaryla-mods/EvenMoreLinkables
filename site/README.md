# Player site

The site at https://databobek.github.io/EvenMoreLinkables/ shows players what Even More Linkables adds and what it needs. It is not part of the mod: `_PublisherPlus.xml` keeps this folder out of the Workshop upload.

## How it works

`build.py` reads the mod's own files:

- the buildings and research projects in `Defs/`, with inheritance resolved and the English DefInjected text applied,
- the patches in `Patches/`, `1.6/Patches` and `1.7/**/Patches`, to work out which benches each linkable links to and which mods those benches come from,
- `About/About.xml` for the supported versions and dependencies,
- the textures, for the icons.

Every stat, cost, research project and bench list on the site comes from those files, so the tables stay correct when you rebalance. The written text lives in `content/` and has to be updated by hand when a mechanic changes.

The game's own files aren't in the repo, so the labels of the vanilla benches and research projects the mod refers to are typed into `VANILLA_LABELS` and `VANILLA_RESEARCH` at the top of `build.py`. If a patch or research project names a vanilla def that isn't there, the build says so.

## Building and previewing

```
pip install -r site/requirements.txt
python site/build.py --serve
```

Then open http://localhost:8000. The build also lists problems it found, like a building without a texture or a vanilla def without a label. It only ever empties an output folder it created itself.

Pushing to `main` rebuilds and publishes the site through `.github/workflows/pages.yml`. One-time setup on GitHub: **Settings > Pages > Source: GitHub Actions**. Pages only works on a public repository with a free account.

The workflow builds with `--strict`, so a problem fails the run and the live site stays as it was. GitHub emails you when a run fails.

## Where things live

| Path | What |
|---|---|
| `content/*.md` | One Markdown file per page. Front matter sets the home page card (`summary`, and `card` with the defNames whose icons it shows). |
| `build.py` | The top bar groups and page order (`NAV`), which def file goes on which page (`PAGE_OF_FILE`), labels, and the helpers pages can call. |
| `templates/` | Page layout (`base.html`, `page.html`, `home.html`) and the tables (`macros.html`). |
| `static/` | CSS, the menu script and the Audiowide font (OFL). The look matches the Beyond Our Reach site. |
| `screenshots/` | In-game screenshots, if you add any. |

## Helpers a page can call

```
{{ buildings("workshop") }}    the buildings table for a page: workshop, laboratory or power
{{ research_table() }}         every research project, with what it unlocks
{{ mod_list() }}               the mods whose benches get linkables
{{ value("HobbesLink_RobotAssistant", "work speed", "pct") }}
                               a number from one building, inside a sentence. Fields: a stat label
                               ("work speed", "research speed", "power output"), limit, distance, power,
                               quality, or the robot's radius, tickInterval, reductionAmt.
                               fmt: num, pct (an offset as +40%), seconds (from ticks)
{{ requirements() }}           RimWorld versions and dependencies, from About.xml
{{ screenshot("name", "caption") }}   site/screenshots/name.png, once you add it
```

Prefer `value()` over typing a number into the text: when the def changes, the page follows.
