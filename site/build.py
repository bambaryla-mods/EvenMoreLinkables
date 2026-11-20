"""Build the Even More Linkables player site.

    python site/build.py            # writes site/_build
    python site/build.py --serve    # builds, then serves it on http://localhost:8000
    python site/build.py --out DIR  # writes somewhere else (CI uses this)

Pages are the Markdown files in site/content. They can call the helpers in
Helpers below, e.g. {{ buildings("workshop") }}, to pull tables generated from
the mod's defs and patches into the text.
"""
from __future__ import annotations

import argparse
import copy
import functools
import http.server
import re
import shutil
import sys
from pathlib import Path

import markdown
from jinja2 import Environment, FileSystemLoader, StrictUndefined, select_autoescape
from lxml import etree
from markupsafe import Markup, escape
from PIL import Image

SITE = Path(__file__).resolve().parent
ROOT = SITE.parent
BUILD_MARKER = ".eml-site-build"  # written into the output so a rebuild only ever deletes its own folder

GITHUB = "https://github.com/databobek/EvenMoreLinkables"
SITE_URL = "https://databobek.github.io/EvenMoreLinkables/"  # for link previews
SCREENSHOT_EXTS = (".png", ".jpg", ".jpeg", ".webp")
ICON_SIZE = 128

# Top bar groups and the pages inside each, in reading order.
NAV = [
    ("Linkables", [("workshop", "Workshop"), ("laboratory", "Laboratory"), ("power", "Power")]),
    ("Research", [("research", "Research")]),
    ("Compatibility", [("compatibility", "Mods & settings")]),
]

# Which page shows the buildings from each def file.
PAGE_OF_FILE = {"MachiningLinkables": "workshop", "ResearchLinkables": "laboratory", "PowerLinkables": "power"}

STAT_LABELS = {
    "WorkTableWorkSpeedFactor": "work speed",
    "ResearchSpeedFactor": "research speed",
    "PowerOutputFactor": "power output",
}
COST_LABELS = {  # singular, plural
    "Steel": ("steel", "steel"),
    "Plasteel": ("plasteel", "plasteel"),
    "ComponentIndustrial": ("component", "components"),
    "ComponentSpacer": ("advanced component", "advanced components"),
}
# Labels of the vanilla and DLC defs the mod refers to. Its patches and research name
# these by defName, and the game's own files aren't in this repo.
VANILLA_LABELS = {
    "TableMachining": "machining table",
    "FabricationBench": "fabrication bench",
    "ElectricSmithy": "electric smithy",
    "FueledSmithy": "fueled smithy",
    "ElectricTailoringBench": "electric tailor bench",
    "HandTailoringBench": "hand tailor bench",
    "TableSculpting": "art bench",
    "TableStonecutter": "stonecutter's table",
    "ElectricSmelter": "electric smelter",
    "ElectricCrematorium": "electric crematorium",
    "SimpleResearchBench": "simple research bench",
    "HiTechResearchBench": "hi-tech research bench",
    "BiofuelRefinery": "biofuel refinery",
    "DrugLab": "drug lab",
    "BioferriteShaper": "bioferrite shaper (Anomaly)",
}
VANILLA_RESEARCH = {
    "Smithing": "smithing",
    "Machining": "machining",
    "Electricity": "electricity",
    "MicroelectronicsBasics": "microelectronics",
    "Fabrication": "fabrication",
    "AdvancedFabrication": "advanced fabrication",
}
DLC_NAMES = {"Royalty", "Ideology", "Biotech", "Anomaly", "Odyssey"}


def fmt_num(v, digits: int = 2) -> str:
    s = f"{v:,.{digits}f}"
    if "." in s:
        s = s.rstrip("0").rstrip(".")
    return s


def slugify(s: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", s.lower()).strip("-")


def split_front_matter(text: str) -> tuple[dict, str]:
    meta: dict[str, str] = {}
    if text.startswith("---\n"):
        head, _, body = text[4:].partition("\n---\n")
        for line in head.splitlines():
            key, sep, value = line.partition(":")
            if sep:
                meta[key.strip()] = value.strip()
        return meta, body
    return meta, text


def clean_desc(text: str | None) -> str:
    """Game text uses literal \\n. The first paragraph of a linkable's description repeats
    its placement rule and limit, which the tables show, so it is dropped."""
    paras = [p.strip() for p in (text or "").replace("\\n", "\n").split("\n\n") if p.strip()]
    if len(paras) > 1 and re.match(r"(Must be placed|Limit)", paras[0]):
        paras = paras[1:]
    return "\n\n".join(paras)


# ---------- reading the mod ----------

def kids(el, path: str) -> list:
    """Child elements of el.find(path), or [] when it's missing."""
    node = el.find(path)
    return [c for c in node if isinstance(c.tag, str)] if node is not None else []


def merge(base, child):
    """RimWorld's def inheritance: lists append, nodes with children merge, values replace."""
    for c in child:
        if not isinstance(c.tag, str):
            continue
        if c.tag == "li":
            base.append(copy.deepcopy(c))
            continue
        existing = base.find(c.tag)
        if existing is not None and len(c) and c.get("Inherit", "").lower() != "false":
            merge(existing, c)
            continue
        if existing is not None:
            base.remove(existing)
        base.append(copy.deepcopy(c))


def load_defs() -> list[tuple[Path, etree._Element]]:
    named, concrete = {}, []
    for f in sorted((ROOT / "Defs").rglob("*.xml")):
        for el in etree.parse(str(f)).getroot():
            if not isinstance(el.tag, str):
                continue
            if el.get("Name"):
                named[el.get("Name")] = el
            if el.get("Abstract", "").lower() != "true":
                concrete.append((f, el))

    def resolve(el):
        parent = named.get(el.get("ParentName", ""))
        if parent is None:  # no parent, or a vanilla one like BuildingBase
            return copy.deepcopy(el)
        out = resolve(parent)
        merge(out, el)
        return out

    return [(f, resolve(el)) for f, el in concrete]


def load_injected(lang: str = "English") -> dict[str, str]:
    out = {}
    for f in sorted((ROOT / "Languages" / lang / "DefInjected").rglob("*.xml")):
        for el in etree.parse(str(f)).getroot():
            if isinstance(el.tag, str) and el.text:
                out[el.tag] = el.text
    return out


def patch_files() -> list[Path]:
    files = sorted((ROOT / "Patches").glob("*.xml"))
    for d in sorted(ROOT.glob("1.*")):
        files += sorted(d.rglob("Patches/*.xml"))
    return files


def load_links() -> list[dict]:
    """Every patch that makes a bench or generator accept this mod's linkables:
    {mods: names the patch needs (empty for vanilla), targets: defNames or a description, facilities}."""
    out, seen = [], set()

    def walk(op, mods):
        cls = op.get("Class") or ""
        if cls == "PatchOperationFindMod":
            match = op.find("match")
            if match is not None:
                walk(match, mods + tuple(li.text.strip() for li in op.findall("mods/li")))
            return
        if cls == "PatchOperationSequence":
            for li in op.findall("operations/li"):
                walk(li, mods)
            return
        if cls == "PatchOperationConditional":
            for sub in (op.find("match"), op.find("nomatch")):
                if sub is not None:
                    walk(sub, mods)
            return
        facilities = sorted({li.text.strip() for li in op.findall("value//li")
                             if li.text and li.text.strip().startswith("HobbesLink_")})
        if not facilities:
            return
        xpath = op.findtext("xpath") or ""
        if "basePowerConsumption" in xpath:
            targets = ["*generators*"]
        else:
            targets = re.findall(r'defName\s*=\s*"([^"]+)"', xpath) or ["*base*"]
        for t in targets:
            key = (mods, t, tuple(facilities))
            if key not in seen:
                seen.add(key)
                out.append({"mods": mods, "target": t, "facilities": facilities})

    for f in patch_files():
        for op in etree.parse(str(f)).getroot():
            if isinstance(op.tag, str):
                walk(op, ())
    return out


def mod_group(mods: tuple[str, ...]) -> str:
    """A patch that checks for several mod names (or one name spelled two ways) is one entry."""
    names: dict[str, str] = {}
    for m in mods:  # of two spellings, keep the one with more capitals ("Kraltech Industries")
        old = names.get(m.lower())
        if old is None or sum(c.isupper() for c in m) > sum(c.isupper() for c in old):
            names[m.lower()] = m
    return " / ".join(names.values())


def cap(s: str) -> str:
    """Capitalize the first letter only: "CNC machine" stays as it is."""
    return s[:1].upper() + s[1:]


class Mod:
    """The mod's buildings, research and links, ready for the templates."""

    def __init__(self, tex_out: Path, problems: list[str]):
        self.problems = problems
        self.tex_out = tex_out
        self.injected = load_injected()
        self.links = load_links()
        defs = load_defs()
        self.research = self._research([d for _, d in defs if d.tag == "ResearchProjectDef"])
        self.buildings = self._buildings([(f, d) for f, d in defs if d.tag == "ThingDef"])

    def text(self, def_name: str, field: str, el) -> str:
        return self.injected.get(f"{def_name}.{field}") or el.findtext(field) or ""

    def research_label(self, def_name: str) -> str:
        if def_name in self.research:
            return self.research[def_name]["label"]
        if def_name in VANILLA_RESEARCH:
            return VANILLA_RESEARCH[def_name]
        self.problems.append(f"no label for research {def_name}; add it to VANILLA_RESEARCH")
        return def_name

    def _research(self, defs) -> dict[str, dict]:
        out = {}
        for d in defs:
            name = d.findtext("defName")
            out[name] = {"def": name, "label": self.text(name, "label", d),
                         "description": self.text(name, "description", d),
                         "cost": float(d.findtext("baseCost") or 0), "tech": d.findtext("techLevel") or "",
                         "prereq_defs": [li.text for li in d.findall("prerequisites/li")], "unlocks": []}
        self.research = out
        for r in out.values():
            r["prerequisites"] = [self.research_label(p) for p in r["prereq_defs"]]
        return out

    def _buildings(self, defs) -> list[dict]:
        out = []
        for f, d in defs:
            name = d.findtext("defName")
            fac = d.find("comps/li[@Class='CompProperties_Facility']")
            if fac is None:
                continue
            page = PAGE_OF_FILE.get(f.stem)
            if page is None:
                self.problems.append(f"{f.name}: no page for this def file; add it to PAGE_OF_FILE")
                continue
            effects = [(STAT_LABELS.get(s.tag, s.tag), float(s.text)) for s in kids(fac, "statOffsets")]
            quality = d.findtext("comps/li[@Class='EvenMoreLinkables.CompProperties_QualityOffset']/qualityOffset")
            helper = d.find("comps/li[@Class='Hobbes.MoreLinkablesProduction.CompProperties_WorkHelper']")
            power = d.findtext("comps/li[@Class='CompProperties_Power']/basePowerConsumption")
            research = [li.text for li in d.findall("researchPrerequisites/li")]
            b = {
                "def": name,
                "page": page,
                "label": self.text(name, "label", d),
                "description": clean_desc(self.text(name, "description", d)),
                "icon": self.icon(d),
                "effects": effects,
                "quality": int(quality) if quality else 0,
                "helper": {k: float(helper.findtext(k) or default) for k, default in
                           (("radius", 5), ("tickInterval", 100), ("reductionAmt", 100))} if helper is not None else None,
                "limit": int(fac.findtext("maxSimultaneous") or 1),
                "adjacent": (fac.findtext("mustBePlacedAdjacent") or "").lower() == "true",
                "distance": float(fac.findtext("maxDistance") or 8),
                "power": float(power) if power else 0,
                "size": "×".join(x.strip() for x in (d.findtext("size") or "(1,1)").strip("()").split(",")),
                "cost": [self.cost(name, c.tag, int(c.text)) for c in kids(d, "costList")],
                "research": [self.research_label(r) for r in research],
            }
            for r in research:
                if r in self.research:
                    self.research[r]["unlocks"].append(b["label"])
            out.append(b)
        self._add_links(out)
        for b in out:
            b["benches"].sort(key=lambda s: ("(" in s, s))
            b["mod_count"] = len(b.pop("mods"))
        return out

    def cost(self, def_name: str, tag: str, n: int) -> tuple[str, int]:
        if tag not in COST_LABELS:
            self.problems.append(f"{def_name}: no label for cost {tag}; add it to COST_LABELS")
            return tag, n
        return COST_LABELS[tag][0 if n == 1 else 1], n

    def _add_links(self, buildings):
        by_def = {b["def"]: b for b in buildings}
        for b in buildings:
            b["benches"], b["mods"] = [], set()
        for link in self.links:
            vanilla = all(m in DLC_NAMES for m in link["mods"])
            for fdef in link["facilities"]:
                b = by_def.get(fdef)
                if b is None:
                    self.problems.append(f"a patch links {fdef}, which isn't a linkable in Defs")
                    continue
                if not vanilla:
                    b["mods"].add(mod_group(link["mods"]))
                elif link["target"] in VANILLA_LABELS:
                    if VANILLA_LABELS[link["target"]] not in b["benches"]:
                        b["benches"].append(VANILLA_LABELS[link["target"]])
                elif link["target"] != "*generators*":
                    self.problems.append(f"no label for vanilla def {link['target']}; add it to VANILLA_LABELS")

    def icon(self, d) -> str | None:
        """The build menu icon, trimmed and scaled down: uiIconPath, else the south-facing graphic."""
        name = d.findtext("defName")
        paths = []
        if d.findtext("uiIconPath"):
            paths.append(d.findtext("uiIconPath"))
        tex = d.findtext("graphicData/texPath")
        if tex:
            paths += [tex + s for s in ("_south", "_east", "_north", "")]
        for p in paths:
            src = ROOT / "Textures" / f"{p}.png"
            if src.is_file():
                dst = self.tex_out / f"{name}.png"
                dst.parent.mkdir(parents=True, exist_ok=True)
                with Image.open(src) as im:
                    im = im.convert("RGBA")
                    box = im.getbbox()
                    if box:
                        im = im.crop(box)
                    im.thumbnail((ICON_SIZE, ICON_SIZE))
                    im.save(dst)
                return f"assets/icons/{name}.png"
        self.problems.append(f"{name}: no texture found (tried {', '.join(paths)})")
        return None

    def mod_integrations(self) -> list[dict]:
        """Mods whose benches get linkables, with how many benches and which kind."""
        groups: dict[str, dict] = {}
        for link in self.links:
            if all(m in DLC_NAMES for m in link["mods"]):
                continue
            key = mod_group(link["mods"])
            g = groups.setdefault(key, {"name": key, "targets": set(), "kinds": set()})
            g["targets"].add(link["target"])
            fac = set(link["facilities"])
            if "HobbesLink_CNCMachine" in fac:
                g["kinds"].add("workshop")
            elif "HobbesLink_PersonalComputer" in fac:
                g["kinds"].add("research")
            else:
                g["kinds"].add("lab equipment")
        out = []
        for g in groups.values():
            shared = "*base*" in g["targets"]
            count = len(g["targets"] - {"*base*"})
            benches = "all its crafting benches" if shared else f"{count} bench{'es' if count != 1 else ''}"
            out.append({"name": g["name"], "benches": benches, "kinds": sorted(g["kinds"])})
        return sorted(out, key=lambda m: m["name"].lower().lstrip("["))


class Helpers:
    """Functions the Markdown pages can call. Each returns a placeholder that
    is swapped for real HTML after Markdown runs, so Markdown never mangles it."""

    def __init__(self, builder: "Builder"):
        self.b = builder

    def _macro(self, name: str, **kw) -> Markup:
        key = f"EMLBLOCK{len(self.b.blocks)}X"
        self.b.blocks[key] = str(getattr(self.b.macros, name)(**kw))
        return Markup(f"\n\n{key}\n\n")

    def buildings(self, page: str):
        rows = [b for b in self.b.mod.buildings if b["page"] == page]
        if not rows:
            self.b.problems.append(f"{self.b.current_page}: no buildings for page {page!r}")
        return self._macro("buildings", rows=rows, page=page)

    def research_table(self):
        return self._macro("research_table", rows=list(self.b.mod.research.values()))

    def mod_list(self):
        return self._macro("mod_list", mods=self.b.mod.mod_integrations())

    def value(self, def_name: str, field: str, fmt: str = "num") -> Markup:
        """A number about one building, for use inside a sentence. field: limit, distance, power,
        quality, or a stat label such as "power output"; helper fields radius, tickInterval, reductionAmt.
        fmt: num, pct (an offset as +40%), seconds (from ticks)."""
        b = next((x for x in self.b.mod.buildings if x["def"] == def_name), None)
        v = None
        if b is not None:
            v = dict(b["effects"]).get(field, b.get(field) if field in b else (b["helper"] or {}).get(field))
        if not isinstance(v, (int, float)):
            self.b.problems.append(f"{self.b.current_page}: value({def_name}, {field}) not found")
            return Markup("?")
        out = {"num": lambda x: fmt_num(x), "pct": lambda x: f"+{x * 100:g}%",
               "seconds": lambda x: fmt_num(x / 60, 0)}[fmt](v)
        return Markup(escape(out))

    def requirements(self):
        return self._macro("requirements", deps=self.b.dependencies())

    def screenshot(self, name: str, caption: str = ""):
        for ext in SCREENSHOT_EXTS:
            src = SITE / "screenshots" / f"{name}{ext}"
            if src.is_file():
                url = f"assets/screenshots/{name}{ext}"
                self.b.copy_asset(src, url)
                return self._macro("screenshot", src=url, caption=caption)
        self.b.missing_shots.append((self.b.current_page, name, caption))
        return Markup("")


class Builder:
    def __init__(self, out: Path):
        self.out = out
        self.problems: list[str] = []
        self.missing_shots: list[tuple[str, str, str]] = []
        self.blocks: dict[str, str] = {}
        self.current_page = ""
        self.prepare_out()
        self.mod = Mod(out / "assets" / "icons", self.problems)
        self.env = Environment(
            loader=FileSystemLoader(str(SITE / "templates")),
            autoescape=select_autoescape(["html"]),
            undefined=StrictUndefined,
            trim_blocks=True,
            lstrip_blocks=True,
        )
        self.env.filters.update(num=fmt_num, slug=slugify, cap=cap)
        self.macros = self.env.get_template("macros.html").module
        self.md_env = Environment(autoescape=False, undefined=StrictUndefined)

    def about(self):
        return etree.parse(str(ROOT / "About" / "About.xml")).getroot()

    def dependencies(self) -> list[dict]:
        versions = [li.text.strip() for li in self.about().findall("supportedVersions/li")]
        out = [{"name": "RimWorld " + " or ".join(versions), "url": ""}]
        for li in self.about().findall("modDependencies/li"):
            url = (li.findtext("steamWorkshopUrl") or li.findtext("downloadUrl") or "").strip()
            out.append({"name": (li.findtext("displayName") or "").strip(), "url": url})
        return out

    def workshop_url(self) -> str:
        pid = (ROOT / "About" / "PublishedFileId.txt").read_text(encoding="utf-8").strip()
        return f"https://steamcommunity.com/sharedfiles/filedetails/?id={pid}"

    def copy_asset(self, src: Path, rel: str):
        dst = self.out / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)

    def prepare_out(self):
        """Empty the output folder, but only if this builder made it."""
        out = self.out
        if out in (ROOT, SITE) or out in ROOT.parents:
            raise SystemExit(f"Refusing to build into {out}: pick an empty or new folder.")
        if out.exists():
            if not (out / BUILD_MARKER).is_file() and any(out.iterdir()):
                raise SystemExit(f"Refusing to delete {out}: it isn't empty and wasn't made by this builder.")
            # Empty it rather than delete it: on Windows an editor or file watcher often holds the folder open.
            for child in out.iterdir():
                shutil.rmtree(child) if child.is_dir() else child.unlink()
        (out / "assets").mkdir(parents=True)
        (out / BUILD_MARKER).write_text("Output of site/build.py. Safe to delete.\n", encoding="utf-8")

    def render_markdown(self, slug: str) -> tuple[dict, str, list]:
        self.current_page = slug
        meta, body = split_front_matter((SITE / "content" / f"{slug}.md").read_text(encoding="utf-8"))
        helpers = Helpers(self)
        g = {name: getattr(helpers, name) for name in dir(helpers)
             if not name.startswith("_") and callable(getattr(helpers, name))}
        text = self.md_env.from_string(body).render(**g)
        md = markdown.Markdown(extensions=["toc", "tables", "attr_list", "md_in_html"],
                               extension_configs={"toc": {"toc_depth": "2-3"}})
        html = md.convert(text)
        for key, block in self.blocks.items():
            html = html.replace(f"<p>{key}</p>", block).replace(key, block)
        toc = [{"id": t["id"], "name": t["name"]} for t in md.toc_tokens if t["level"] == 2]
        return meta, html, toc

    def build(self):
        shutil.copytree(SITE / "static", self.out / "assets", dirs_exist_ok=True)
        self.copy_asset(ROOT / "About" / "Preview.png", "assets/preview.png")
        self.copy_asset(ROOT / "Textures" / "UI" / "WorkReducer.png", "assets/favicon.png")

        common = {"nav": NAV, "workshop": self.workshop_url(), "github": GITHUB, "site_url": SITE_URL}
        flat = [{"slug": s, "title": t, "group": g} for g, entries in NAV for s, t in entries]
        cards = []
        for i, page in enumerate(flat):
            meta, html, toc = self.render_markdown(page["slug"])
            group_pages = next(entries for g, entries in NAV if g == page["group"])
            out_html = self.env.get_template("page.html").render(
                **common, page=page, meta=meta, content=Markup(html), toc=toc, active_group=page["group"],
                group_pages=group_pages,
                prev=flat[i - 1] if i > 0 else None,
                next=flat[i + 1] if i + 1 < len(flat) else None,
            )
            (self.out / f"{page['slug']}.html").write_text(out_html, encoding="utf-8")
            cards.append({**page, "summary": meta.get("summary", ""), "icons": self.card_icons(meta.get("card", ""))})

        meta, html, _ = self.render_markdown("index")
        hero_icons = self.card_icons(meta.get("icons", ""))
        index = self.env.get_template("home.html").render(**common, meta=meta, content=Markup(html), cards=cards,
                                                          hero_icons=hero_icons, active_group="")
        (self.out / "index.html").write_text(index, encoding="utf-8")
        (self.out / ".nojekyll").write_text("", encoding="utf-8")

    def card_icons(self, spec: str) -> list[str]:
        """Front matter "card:" lists the defNames whose icons make the home page thumbnail."""
        icons = {b["def"]: b["icon"] for b in self.mod.buildings}
        out = []
        for name in filter(None, (s.strip() for s in spec.split(","))):
            if icons.get(name):
                out.append(icons[name])
            else:
                self.problems.append(f"card icon {name} not found")
        return out

    def report(self) -> int:
        if self.problems:
            print(f"{len(self.problems)} problem(s):")
            for n in self.problems:
                print("  -", n)
        if self.missing_shots:
            print(f"{len(self.missing_shots)} screenshot(s) not added yet (put them in site/screenshots/):")
            for page, name, caption in self.missing_shots:
                print(f"  - {name}.png  ({page}: {caption})")
        print(f"Built {self.out}")
        return len(self.problems)


def serve(out: Path, port: int):
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=str(out))
    with http.server.ThreadingHTTPServer(("127.0.0.1", port), handler) as httpd:
        print(f"Serving on http://localhost:{port}/ (Ctrl+C to stop)")
        httpd.serve_forever()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", type=Path, default=SITE / "_build")
    ap.add_argument("--serve", nargs="?", const=8000, type=int, metavar="PORT")
    ap.add_argument("--strict", action="store_true", help="exit with an error if the build reports problems (CI)")
    args = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    b = Builder(args.out.resolve())
    b.build()
    problems = b.report()
    if args.strict and problems:
        raise SystemExit(f"--strict: {problems} problem(s)")
    if args.serve:
        serve(args.out.resolve(), args.serve)


if __name__ == "__main__":
    main()
