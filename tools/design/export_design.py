"""Exporte la maquette Claude Design et le design system Strata en HTML statique + zip.

Pourquoi ce script : les fichiers `.dc.html` d'un canvas Claude Design ne s'affichent qu'avec
le runtime du canvas (boucles `<sc-for>`, composants `<x-import>` montés par React). Codex doit
pouvoir lire chaque écran hors ligne, sans runtime : on rend donc chaque artboard dans Chrome
headless, puis on garde le DOM rendu, sans aucun script.

Prérequis :
  - Python 3.11+ ;
  - Google Chrome ou Microsoft Edge installé (chemin détecté, ou --chrome) ;
  - les fichiers du canvas et du design system relus depuis claude.ai par Claude
    (Artifact `read` avec `paths` et `out_dir`), avec `artifact-type/dc-runtime.js`.

Usage :
  python tools/design/export_design.py maquette <dossier canvas> <dest> --runtime <dc-runtime.js> \
      --url <lien du canvas> --title "<titre>"
  python tools/design/export_design.py strata <dossier design system> <dest> --runtime <dc-runtime.js> \
      --url <lien du design system>

<dossier canvas> contient `project/canvas.json`, `project/*.dc.html` et `project/ds/strata/…`.
<dossier design system> contient `project/README.md`, `project/tokens.json`, `project/components/…`.

Produit dans <dest> :
  maquette : preview/<Ecran>.html (rendu statique), preview/index.html (sommaire par page du
             canvas), preview/ds/… (styles des composants), sources/ (fichiers du canvas tels
             quels) et <dest>.zip.
  strata   : fichiers du design system tels quels, rendered/<Composant>.html, index.html
             (tokens, typographie, composants) et <dest>.zip.
"""
from __future__ import annotations

import argparse
import functools
import html
import http.server
import json
import os
import re
import shutil
import socketserver
import subprocess
import sys
import tempfile
import threading
import zipfile
from pathlib import Path

CHROME_CANDIDATES = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "/usr/bin/google-chrome",
    "/usr/bin/chromium",
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
]

SCRIPT_RE = re.compile(r"<script\b[^>]*>.*?</script>", re.S | re.I)
TPL_ATTR_RE = re.compile(r'\sdata-dc-tpl="[^"]*"')
DC_LINK_RE = re.compile(r'href="([A-Za-z0-9_-]+)\.dc\.html"')


# Certains artboards passent `options="a,b,c"` (Segmented) ou `items="a,b,c"` (Tabs) en chaîne
# au lieu d'un tableau : le composant plante (`options.map is not a function`) et tout l'artboard
# disparaît. Ce correctif, chargé après le bundle et seulement pour le rendu, convertit ces chaînes
# en tableaux. Défaut corrigé dans le canvas le 2026-10-04 (version 28) : ce correctif reste en filet
# de sécurité pour les prochaines révisions de la maquette.
STRATA_SHIM = """<script>
(function () {
  var S = window.Strata, h = window.React && window.React.createElement;
  if (!S || !h) { return; }
  function split(v) { return String(v).split(',').map(function (s) { return s.trim(); }).filter(Boolean); }
  var Seg = S.Segmented, Tabs = S.Tabs;
  S.Segmented = function (p) {
    if (typeof p.options !== 'string') { return h(Seg, p); }
    var opts = split(p.options).map(function (s) { return { value: s, label: s }; });
    return h(Seg, Object.assign({}, p, { options: opts }));
  };
  S.Tabs = function (p) {
    if (typeof p.items !== 'string') { return h(Tabs, p); }
    var items = split(p.items).map(function (s) { return { id: s, label: s }; });
    return h(Tabs, Object.assign({}, p, { items: items }));
  };
})();
</script>"""


def inject_shim(directory: Path) -> None:
    for page in directory.glob("*.dc.html"):
        text = page.read_text(encoding="utf-8")
        marker = '<script src="ds/strata/components/bundle.js"></script>'
        if marker in text:
            page.write_text(text.replace(marker, marker + STRATA_SHIM, 1), encoding="utf-8")


def find_chrome(explicit: str | None) -> str:
    if explicit:
        return explicit
    for candidate in CHROME_CANDIDATES:
        if Path(candidate).exists():
            return candidate
    sys.exit("Chrome ou Edge introuvable : passez --chrome <chemin>.")


class QuietHandler(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *args):  # noqa: D401 - silence
        pass


def serve(directory: Path) -> tuple[socketserver.TCPServer, int]:
    handler = functools.partial(QuietHandler, directory=str(directory))
    server = socketserver.TCPServer(("127.0.0.1", 0), handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    return server, server.server_address[1]


def render(chrome: str, url: str, width: int, height: int) -> str:
    with tempfile.TemporaryDirectory() as profile:
        result = subprocess.run(
            [
                chrome,
                "--headless=new",
                "--disable-gpu",
                "--no-first-run",
                "--no-default-browser-check",
                f"--user-data-dir={profile}",
                f"--window-size={width},{height}",
                "--virtual-time-budget=10000",
                "--dump-dom",
                url,
            ],
            capture_output=True,
            timeout=120,
        )
    dom = result.stdout.decode("utf-8", errors="replace")
    if "<body" not in dom:
        sys.exit(f"Rendu vide pour {url} : {result.stderr.decode('utf-8', errors='replace')[:400]}")
    return dom


def to_static(dom: str) -> str:
    dom = SCRIPT_RE.sub("", dom)
    dom = TPL_ATTR_RE.sub("", dom)
    dom = DC_LINK_RE.sub(r'href="\1.html"', dom)
    if "<!DOCTYPE" not in dom[:50].upper():
        dom = "<!doctype html>\n" + dom
    return dom


def check_static(name: str, dom: str) -> list[str]:
    problems = []
    if "{{" in dom:
        problems.append("gabarit {{…}} non résolu")
    if "<x-import" in dom or "<sc-for" in dom or "<dc-import" in dom:
        problems.append("balise du runtime non rendue")
    if "sc-logic-error" in dom and 'class="sc-logic-error"' in dom:
        problems.append("erreur de logique du composant")
    return [f"{name} : {p}" for p in problems]


def zip_dir(source: Path, zip_path: Path) -> None:
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(source.rglob("*")):
            if path.is_file():
                archive.write(path, (Path(source.name) / path.relative_to(source)).as_posix())


# --------------------------------------------------------------------------- maquette


def export_maquette(args) -> None:
    chrome = find_chrome(args.chrome)
    project = Path(args.source) / "project"
    canvas = json.loads((project / "canvas.json").read_text(encoding="utf-8"))
    dest = Path(args.dest)
    for sub in ("preview", "sources"):
        if (dest / sub).exists():
            shutil.rmtree(dest / sub)
    (dest / "preview").mkdir(parents=True)

    shutil.copytree(project, dest / "sources", ignore=shutil.ignore_patterns("support.js"))
    shutil.copytree(project / "ds", dest / "preview" / "ds", ignore=shutil.ignore_patterns("*.js"))

    with tempfile.TemporaryDirectory() as work:
        work_dir = Path(work)
        shutil.copytree(project, work_dir, dirs_exist_ok=True)
        shutil.copy(args.runtime, work_dir / "support.js")
        inject_shim(work_dir)
        server, port = serve(work_dir)
        try:
            pages ={p["id"]: p["name"] for p in canvas.get("pages", [])}
            default_page = next(iter(pages)) if pages else "main"
            rows: dict[str, list] = {pid: [] for pid in pages} or {default_page: []}
            problems: list[str] = []
            order = canvas.get("order") or sorted(canvas["boards"])
            for file_name in order:
                board = canvas["boards"][file_name]
                stem = file_name.removesuffix(".dc.html")
                width, height = int(board.get("w", 1440)), int(board.get("h", 900))
                dom = to_static(render(chrome, f"http://127.0.0.1:{port}/{file_name}", width, height))
                problems.extend(check_static(stem, dom))
                (dest / "preview" / f"{stem}.html").write_text(dom, encoding="utf-8")
                rows.setdefault(board.get("page", default_page), []).append(
                    (stem, board.get("title", stem), width, height)
                )
                print(f"  {stem:28} {width}x{height}  {board.get('title', stem)}")
        finally:
            server.shutdown()

    notes = [n for n in canvas.get("notes", {}).values() if not n.get("kind")]
    parts = [
        "<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\">",
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">",
        f"<title>Maquette — {html.escape(args.title)}</title>",
        "<style>body{margin:0;padding:40px;background:#0b0e13;color:#e8ecf2;font:14px/20px system-ui,sans-serif}"
        "a{color:#7fd6e8}h1{font-size:28px;font-weight:600}h2{font-size:16px;margin-top:32px;color:#a7b0bf;"
        "text-transform:uppercase;letter-spacing:.06em}table{border-collapse:collapse;width:100%;background:#11151c}"
        "td,th{border:1px solid #252c38;padding:8px 12px;text-align:left}code{font-family:ui-monospace,monospace;"
        "font-size:12px;color:#a7b0bf}.note{background:#11151c;border:1px solid #252c38;padding:12px;margin:8px 0}</style>",
        "</head><body>",
        f"<h1>Maquette — {html.escape(args.title)}</h1>",
        f"<p>Export statique du canvas Claude Design <a href=\"{args.url}\">{args.url}</a>. Chaque page est "
        "autonome et s'ouvre sans réseau (hors polices Google). La référence de style est le design system "
        "Strata (<code>docs/design/strata/</code>).</p>",
    ]
    for pid, page_rows in rows.items():
        parts.append(f"<h2>{html.escape(pages.get(pid, pid))}</h2><table><tr><th>Écran</th><th>Fichier</th><th>Taille</th></tr>")
        for stem, title, width, height in page_rows:
            parts.append(
                f"<tr><td><a href=\"{stem}.html\">{html.escape(title)}</a></td>"
                f"<td><code>preview/{stem}.html</code></td><td>{width} × {height}</td></tr>"
            )
        parts.append("</table>")
    parts.extend(f"<div class=\"note\">{html.escape(n['text'])}</div>" for n in notes)
    parts.append("</body></html>")
    (dest / "preview" / "index.html").write_text("\n".join(parts), encoding="utf-8")

    zip_dir(dest, dest.parent / f"{dest.name}.zip")
    print(f"{sum(len(r) for r in rows.values())} écrans exportés dans {dest / 'preview'}")
    if problems:
        print("ANOMALIES :")
        for problem in problems:
            print("  " + problem)
        sys.exit(1)


# --------------------------------------------------------------------------- strata

HARNESS = """<!doctype html>
<html lang="fr"><head><meta charset="utf-8"><title>{title}</title>
<script src="../support.js"></script>
<link rel="stylesheet" href="../components/bundle.css">
<link rel="stylesheet" href="../tokens.css">
<script src="../components/bundle.js"></script>
</head><body>
{body}
</body></html>
"""


def tokens_css(tokens: dict) -> str:
    lines = ["/* Généré depuis tokens.json par tools/design/export_design.py — ne pas modifier. */", ":root {"]
    for group in ("color", "spacing", "radius", "shadow", "size"):
        for token in tokens.get(group, {}).get("tokens", []):
            lines.append(f"  --{token['name']}: {token['value']};")
    lines.append("}")
    return "\n".join(lines) + "\n"


def export_strata(args) -> None:
    chrome = find_chrome(args.chrome)
    project = Path(args.source) / "project"
    dest = Path(args.dest)
    if dest.exists():
        shutil.rmtree(dest)
    shutil.copytree(project, dest)
    tokens = json.loads((project / "tokens.json").read_text(encoding="utf-8"))
    (dest / "tokens.css").write_text(tokens_css(tokens), encoding="utf-8")

    components = sorted(p.parent.name for p in (dest / "components").glob("*/preview.html"))
    (dest / "rendered").mkdir()
    problems: list[str] = []
    with tempfile.TemporaryDirectory() as work:
        work_dir = Path(work)
        shutil.copytree(dest, work_dir, dirs_exist_ok=True)
        shutil.copy(args.runtime, work_dir / "support.js")
        (work_dir / "harness").mkdir()
        for name in components:
            source = (dest / "components" / name / "preview.html").read_text(encoding="utf-8")
            body = re.search(r"<body>(.*)</body>", source, re.S)
            head_style = re.search(r"<style>(.*?)</style>", source, re.S)
            content = (f"<style>{head_style.group(1)}</style>" if head_style else "") + (body.group(1) if body else source)
            (work_dir / "harness" / f"{name}.html").write_text(
                HARNESS.format(title=f"{name} — Strata", body=content), encoding="utf-8"
            )
        server, port = serve(work_dir)
        try:
            for name in components:
                dom = to_static(render(chrome, f"http://127.0.0.1:{port}/harness/{name}.html", 1100, 800))
                dom = dom.replace('href="../components/bundle.css"', 'href="../components/bundle.css"')
                problems.extend(check_static(name, dom))
                if 'id="root"' in dom and re.search(r'<div id="root"[^>]*>\s*</div>', dom):
                    problems.append(f"{name} : rendu vide")
                (dest / "rendered" / f"{name}.html").write_text(dom, encoding="utf-8")
                print(f"  {name}")
        finally:
            server.shutdown()

    colors = tokens.get("color", {}).get("tokens", [])
    swatches = "".join(
        f"<div class=\"sw\"><span style=\"background:{html.escape(t['value'])}\"></span><b>{html.escape(t['name'])}</b>"
        f"<code>{html.escape(t['value'])}</code><small>{html.escape(t.get('usage', ''))}</small></div>"
        for t in colors
    )
    scale = "".join(
        f"<tr><td><code>{html.escape(t['name'])}</code></td><td><code>{html.escape(t['value'])}</code></td>"
        f"<td>{html.escape(t.get('usage', ''))}</td></tr>"
        for group in ("spacing", "radius", "shadow", "size")
        for t in tokens.get(group, {}).get("tokens", [])
    )
    cards = "".join(
        f"<section><h3>{name}</h3><p><a href=\"components/{name}/README.md\">Règles d'usage</a> · "
        f"<a href=\"rendered/{name}.html\">Aperçu seul</a></p>"
        f"<iframe src=\"rendered/{name}.html\" title=\"{name}\" loading=\"lazy\"></iframe></section>"
        for name in components
    )
    index = f"""<!doctype html><html lang="fr"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1"><title>Strata — design system IFS</title>
<link rel="stylesheet" href="tokens.css">
<style>
body{{margin:0;padding:40px;background:var(--bg);color:var(--text);font:14px/20px "Instrument Sans",system-ui,sans-serif}}
a{{color:var(--signal-text)}}h1{{font-size:28px;font-weight:600}}h2{{margin-top:40px;font-size:16px;text-transform:uppercase;letter-spacing:.06em;color:var(--text-2)}}
.grid{{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:12px}}
.sw{{display:grid;grid-template-columns:40px 1fr;gap:2px 10px;padding:10px;background:var(--surface-1);border:1px solid var(--line);border-radius:10px}}
.sw span{{grid-row:span 3;width:40px;height:40px;border-radius:6px;border:1px solid var(--line-strong)}}
.sw small{{color:var(--text-3)}}code{{font-family:ui-monospace,monospace;font-size:12px;color:var(--text-2)}}
table{{border-collapse:collapse;width:100%;background:var(--surface-1)}}td{{border:1px solid var(--line);padding:6px 10px}}
section{{margin:16px 0}}iframe{{width:100%;height:420px;border:1px solid var(--line);border-radius:10px;background:var(--bg)}}
</style></head><body>
<h1>Strata — design system d'InfraFlowSculptor</h1>
<p>Export statique du design system <a href="{args.url}">{args.url}</a>. Les règles complètes sont dans
<a href="README.md">README.md</a> ; la source des valeurs est <code>tokens.json</code> (variables CSS dans
<code>tokens.css</code>, générées) ; la géométrie exacte des composants est dans <code>components/bundle.css</code>.</p>
<h2>Couleurs</h2><div class="grid">{swatches}</div>
<h2>Espacements, rayons, ombres, tailles</h2><table>{scale}</table>
<h2>Composants</h2>{cards}
</body></html>
"""
    (dest / "index.html").write_text(index, encoding="utf-8")
    zip_dir(dest, dest.parent / f"{dest.name}.zip")
    print(f"{len(components)} composants rendus dans {dest / 'rendered'}")
    if problems:
        print("ANOMALIES :")
        for problem in problems:
            print("  " + problem)
        sys.exit(1)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("kind", choices=["maquette", "strata"])
    parser.add_argument("source")
    parser.add_argument("dest")
    parser.add_argument("--runtime", required=True, help="artifact-type/dc-runtime.js relu depuis le canvas")
    parser.add_argument("--url", required=True)
    parser.add_argument("--title", default="InfraFlowSculptor v1")
    parser.add_argument("--chrome")
    args = parser.parse_args()
    if args.kind == "maquette":
        export_maquette(args)
    else:
        export_strata(args)


if __name__ == "__main__":
    os.environ.setdefault("PYTHONIOENCODING", "utf-8")
    main()
