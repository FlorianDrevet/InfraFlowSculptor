"""Garde-fou du plan d'implémentation : étape courante, verrous de revue, avancement.

Le plan est la suite des titres d'étapes des fichiers `docs/plan/NN-*.md`, dans l'ordre des noms de
fichiers puis du texte :

    ### S-03 — Titre d'une étape
    ### 🔒 R-01 — Titre d'un verrou de revue

`NEXT.md` (racine) porte l'étape courante et son statut dans son tableau « En un coup d'œil ».
Un verrou `R-nn` est levé par le fichier `docs/plan/revues/R-nn-revue.md` qui contient la ligne
`Verdict : APPROUVÉ`, écrit par Claude.

Commandes (toutes en Python 3.11+, sans dépendance) :

  python tools/plan/gate.py status            état courant, prochaine étape, verrous
  python tools/plan/gate.py check             échoue (code 3) si un verrou bloque le travail de code
  python tools/plan/gate.py lint              vérifie la forme du plan (identifiants, rubriques)
  python tools/plan/gate.py done <ID>         Luna : l'étape <ID> est terminée → passe à la suivante
  python tools/plan/gate.py request <R-nn>    Luna : demande la revue (exige R-nn-demande.md)
  python tools/plan/gate.py approve <R-nn>    Claude : lève le verrou (exige « Verdict : APPROUVÉ »)
  python tools/plan/gate.py reject <R-nn>     Claude : corrections demandées (exige « Verdict : CORRECTIONS »)
  python tools/plan/gate.py precommit         hook git : refuse du code tant qu'une revue est en attente
  python tools/plan/gate.py wait-recette <ID>  Luna : la suite attend une recette de l'utilisateur (statut EN_ATTENTE_DE_RECETTE)
  python tools/plan/gate.py resume <ID>        Luna, SEULEMENT quand l'utilisateur transmet des résultats de recette : EN_COURS

Statuts de NEXT.md : A_FAIRE, EN_COURS, EN_ATTENTE_DE_REVUE, EN_ATTENTE_DE_RECETTE, CORRECTIONS_DEMANDEES, BLOQUE.

Un fichier de plan qui contient la ligne « > **Niveau : découpé.** » n'est pas exécutable : `check` refuse toute
étape qu'il contient (code 3) tant que Claude ne l'a pas détaillé (skill detailler-jalon, qui retire cette ligne).
"""
from __future__ import annotations

import datetime as dt
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PLAN_DIR = ROOT / "docs" / "plan"
REVIEW_DIR = PLAN_DIR / "revues"
NEXT = ROOT / "NEXT.md"
JOURNAL = PLAN_DIR / "JOURNAL.md"

STEP_RE = re.compile(r"^###\s+(🔒\s+)?([A-Z][A-Z0-9]*-\d{2})\s+—\s+(.+?)\s*$")
STATUSES = {"A_FAIRE", "EN_COURS", "EN_ATTENTE_DE_REVUE", "EN_ATTENTE_DE_RECETTE", "CORRECTIONS_DEMANDEES", "BLOQUE"}
NOT_DETAILED_MARK = "> **Niveau : découpé.**"
# Pendant une revue en attente, seuls ces chemins peuvent être commités (demande, état, conception).
ALLOWED_WHILE_LOCKED = ("docs/", "NEXT.md", ".github/memory/", "MEMORY.md")
REQUIRED_MARKERS = ("🔧", "✅", "🧪")


@dataclass
class Step:
    ident: str
    title: str
    lock: bool
    file: Path
    line: int
    body: str

    @property
    def anchor(self) -> str:
        text = f"{'🔒 ' if self.lock else ''}{self.ident} — {self.title}".lower()
        text = re.sub(r"[^\w\- ]", "", text, flags=re.UNICODE)
        # Même règle que les ancres GitHub : l'émoji disparaît mais son espace reste (« -r-01--… »).
        return text.rstrip().replace(" ", "-")

    @property
    def link(self) -> str:
        return f"docs/plan/{self.file.name}#{self.anchor}"


def load_steps() -> list[Step]:
    steps: list[Step] = []
    for path in sorted(PLAN_DIR.glob("[0-9][0-9]-*.md")):
        lines = path.read_text(encoding="utf-8").splitlines()
        current: Step | None = None
        buffer: list[str] = []
        for number, line in enumerate(lines, start=1):
            match = STEP_RE.match(line)
            if match or line.startswith("## ") or line.startswith("### "):
                if current:
                    current.body = "\n".join(buffer)
                    steps.append(current)
                    current = None
                buffer = []
            if match:
                current = Step(match.group(2), match.group(3), bool(match.group(1)), path, number, "")
            elif current:
                buffer.append(line)
        if current:
            current.body = "\n".join(buffer)
            steps.append(current)
    return steps


def read_next() -> dict[str, str]:
    if not NEXT.exists():
        fail("NEXT.md introuvable à la racine du dépôt.")
    values: dict[str, str] = {}
    for line in NEXT.read_text(encoding="utf-8").splitlines():
        match = re.match(r"^\|\s*\*\*(.+?)\*\*\s*\|\s*(.*?)\s*\|\s*$", line)
        if match:
            values[match.group(1).strip()] = match.group(2).strip()
    return values


def ident_in(cell: str) -> str | None:
    match = re.search(r"`([A-Z][A-Z0-9]*-\d{2})`", cell or "")
    return match.group(1) if match else None


def status_in(cell: str) -> str:
    match = re.search(r"`([A-Z_]+)`", cell or "")
    return match.group(1) if match else ""


def write_next_row(label: str, value: str) -> None:
    text = NEXT.read_text(encoding="utf-8")
    pattern = re.compile(rf"^(\|\s*\*\*{re.escape(label)}\*\*\s*\|).*?(\|\s*)$", re.M)
    if not pattern.search(text):
        fail(f"Ligne « {label} » absente du tableau de NEXT.md.")
    text = pattern.sub(lambda m: f"{m.group(1)} {value} |", text, count=1)
    NEXT.write_text(text, encoding="utf-8")


def verdict(lock_id: str) -> str | None:
    review = REVIEW_DIR / f"{lock_id}-revue.md"
    if not review.exists():
        return None
    match = re.search(r"^Verdict\s*:\s*\**\s*([A-ZÉ_ ]+?)\**\s*$", review.read_text(encoding="utf-8"), re.M)
    return match.group(1).strip() if match else None


def fail(message: str, code: int = 1) -> None:
    print(f"✖ {message}")
    sys.exit(code)


def describe(step: Step) -> str:
    return f"{'🔒 ' if step.lock else ''}{step.ident} — {step.title}  ({step.link})"


def state() -> tuple[list[Step], int, str]:
    steps = load_steps()
    if not steps:
        fail("Aucune étape trouvée dans docs/plan/NN-*.md.")
    values = read_next()
    current_id = ident_in(values.get("Étape courante", ""))
    status = status_in(values.get("Statut", ""))
    ids = [s.ident for s in steps]
    if current_id not in ids:
        fail(f"Étape courante « {current_id} » de NEXT.md absente du plan.")
    if status not in STATUSES:
        fail(f"Statut « {status} » inconnu. Attendu : {', '.join(sorted(STATUSES))}.")
    return steps, ids.index(current_id), status


def unapproved_locks_before(steps: list[Step], index: int) -> list[Step]:
    return [s for s in steps[:index] if s.lock and verdict(s.ident) != "APPROUVÉ"]


def cmd_status() -> None:
    steps, index, status = state()
    current = steps[index]
    print(f"Étape courante : {describe(current)}")
    print(f"Statut         : {status}")
    nxt = steps[index + 1] if index + 1 < len(steps) else None
    print(f"Étape suivante : {describe(nxt) if nxt else '— fin du plan détaillé'}")
    pending = unapproved_locks_before(steps, index)
    if pending:
        print("Verrous non levés avant l'étape courante : " + ", ".join(s.ident for s in pending))
    upcoming = next((s for s in steps[index:] if s.lock), None)
    if upcoming:
        print(f"Prochain verrou : {upcoming.ident} — {upcoming.title}")


def cmd_check() -> None:
    steps, index, status = state()
    pending = unapproved_locks_before(steps, index)
    if pending:
        fail("Verrou(s) franchi(s) sans revue approuvée : " + ", ".join(s.ident for s in pending)
             + ". Revenir à l'étape du verrou dans NEXT.md.", 3)
    current = steps[index]
    if status == "EN_ATTENTE_DE_REVUE":
        fail(f"Revue {current.ident} en attente : aucun code tant que Claude n'a pas rendu son verdict.", 3)
    if status == "EN_ATTENTE_DE_RECETTE":
        fail(f"{current.ident} attend une recette de l'utilisateur. Reprendre seulement si l'utilisateur transmet des "
             f"résultats : python tools/plan/gate.py resume {current.ident}.", 3)
    if NOT_DETAILED_MARK in current.file.read_text(encoding="utf-8"):
        fail(f"{current.file.name} est seulement découpé : Claude doit le détailler (skill detailler-jalon) avant "
             f"l'exécution de {current.ident}.", 3)
    if status == "BLOQUE":
        fail("Statut BLOQUE : lire la section « Questions pour Claude » de NEXT.md.", 3)
    if current.lock and status not in {"CORRECTIONS_DEMANDEES", "A_FAIRE", "EN_COURS"}:
        fail(f"Statut {status} incohérent sur le verrou {current.ident}.")
    print(f"✔ Travail autorisé sur {describe(current)} (statut {status}).")


def cmd_lint() -> None:
    steps = load_steps()
    problems: list[str] = []
    seen: dict[str, Step] = {}
    for step in steps:
        if step.ident in seen:
            problems.append(f"{step.ident} dupliqué ({seen[step.ident].file.name}:{seen[step.ident].line} et {step.file.name}:{step.line})")
        seen[step.ident] = step
        if step.lock and not step.ident.startswith("R-"):
            problems.append(f"{step.ident} : un verrou doit s'appeler R-nn ({step.file.name}:{step.line})")
        if not step.lock and step.ident.startswith("R-"):
            problems.append(f"{step.ident} : R-nn est réservé aux verrous 🔒 ({step.file.name}:{step.line})")
        if not step.lock:
            for marker in REQUIRED_MARKERS:
                if marker not in step.body:
                    problems.append(f"{step.ident} : rubrique {marker} absente ({step.file.name}:{step.line})")
    values = read_next() if NEXT.exists() else {}
    current = ident_in(values.get("Étape courante", ""))
    if current and current not in seen:
        problems.append(f"NEXT.md pointe vers {current}, absent du plan")
    if problems:
        for problem in problems:
            print("✖ " + problem)
        sys.exit(1)
    print(f"✔ Plan cohérent : {len(steps)} étapes dont {sum(s.lock for s in steps)} verrous.")


def journal(actor: str, event: str, detail: str = "") -> None:
    stamp = dt.datetime.now().strftime("%Y-%m-%d %H:%M")
    with JOURNAL.open("a", encoding="utf-8") as handle:
        handle.write(f"| {stamp} | {actor} | {event} | {detail} |\n")


def today() -> str:
    return dt.date.today().isoformat()


def head_commit() -> str:
    try:
        return subprocess.run(["git", "rev-parse", "--short", "HEAD"], cwd=ROOT, capture_output=True,
                              text=True, check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return "?"


def cmd_done(ident: str, who: str) -> None:
    steps, index, status = state()
    current = steps[index]
    if current.ident != ident:
        fail(f"L'étape courante est {current.ident}, pas {ident}.")
    if current.lock:
        fail("Un verrou ne se termine pas par « done » : request (Luna) puis approve (Claude).")
    if status in {"EN_ATTENTE_DE_REVUE", "BLOQUE"}:
        fail(f"Statut {status} : impossible de terminer l'étape.")
    nxt = steps[index + 1] if index + 1 < len(steps) else None
    commit = head_commit()
    write_next_row("Dernière étape terminée", f"[`{ident}`]({current.link}) — {current.title} (commit `{commit}`)")
    if nxt:
        write_next_row("Étape courante", f"[`{nxt.ident}`]({nxt.link}) — {nxt.title}")
        write_next_row("Statut", "`A_FAIRE`")
        after = steps[index + 2] if index + 2 < len(steps) else None
        write_next_row("Étape suivante", f"`{after.ident}` — {after.title}" if after else "— fin du plan détaillé")
    write_next_row("Dernière mise à jour", f"{today()} — {who}")
    journal(who, f"`{ident}` terminée", f"commit `{commit}`")
    print(f"✔ {ident} terminée. Étape courante : {describe(nxt) if nxt else 'fin du plan détaillé'}")
    if nxt and nxt.lock:
        print("⚠ L'étape suivante est un verrou : appliquer la skill demander-revue, puis s'arrêter.")


def cmd_request(ident: str) -> None:
    steps, index, status = state()
    current = steps[index]
    if current.ident != ident or not current.lock:
        fail(f"{ident} n'est pas le verrou courant (courant : {current.ident}).")
    demand = REVIEW_DIR / f"{ident}-demande.md"
    if not demand.exists():
        fail(f"Écrire d'abord {demand.relative_to(ROOT)} (modèle : docs/plan/revues/README.md).")
    write_next_row("Statut", "`EN_ATTENTE_DE_REVUE`")
    write_next_row("Verrou", f"🔒 `{ident}` — revue demandée le {today()}, voir [{demand.name}](docs/plan/revues/{demand.name})")
    write_next_row("Dernière mise à jour", f"{today()} — Luna")
    journal("Luna", f"revue `{ident}` demandée", demand.name)
    print(f"✔ Revue {ident} demandée. Arrêtez-vous : Claude doit relire avant toute suite.")


def cmd_approve(ident: str) -> None:
    steps, index, _ = state()
    current = steps[index]
    if current.ident != ident or not current.lock:
        fail(f"{ident} n'est pas le verrou courant (courant : {current.ident}).")
    if verdict(ident) != "APPROUVÉ":
        fail(f"docs/plan/revues/{ident}-revue.md doit contenir « Verdict : APPROUVÉ ».")
    nxt = steps[index + 1] if index + 1 < len(steps) else None
    write_next_row("Dernière étape terminée", f"[`{ident}`]({current.link}) — {current.title} (approuvée)")
    write_next_row("Verrou", "aucun")
    if nxt:
        write_next_row("Étape courante", f"[`{nxt.ident}`]({nxt.link}) — {nxt.title}")
        after = steps[index + 2] if index + 2 < len(steps) else None
        write_next_row("Étape suivante", f"`{after.ident}` — {after.title}" if after else "— fin du plan détaillé")
    write_next_row("Statut", "`A_FAIRE`")
    write_next_row("Dernière mise à jour", f"{today()} — Claude (revue {ident})")
    journal("Claude", f"verrou `{ident}` levé", f"{ident}-revue.md")
    print(f"✔ Verrou {ident} levé. Étape courante : {describe(nxt) if nxt else 'fin du plan détaillé'}")


def cmd_reject(ident: str) -> None:
    steps, index, _ = state()
    current = steps[index]
    if current.ident != ident or not current.lock:
        fail(f"{ident} n'est pas le verrou courant (courant : {current.ident}).")
    if verdict(ident) not in {"CORRECTIONS", "CORRECTIONS DEMANDÉES"}:
        fail(f"docs/plan/revues/{ident}-revue.md doit contenir « Verdict : CORRECTIONS ».")
    write_next_row("Statut", "`CORRECTIONS_DEMANDEES`")
    write_next_row("Verrou", f"🔒 `{ident}` — corrections demandées, voir [{ident}-revue.md](docs/plan/revues/{ident}-revue.md)")
    write_next_row("Dernière mise à jour", f"{today()} — Claude (revue {ident})")
    journal("Claude", f"verrou `{ident}` : corrections demandées", f"{ident}-revue.md")
    print(f"✔ Corrections demandées sur {ident}. Luna applique la skill appliquer-corrections.")


def cmd_set_wait(ident: str, waiting: bool) -> None:
    steps, index, status = state()
    current = steps[index]
    if current.ident != ident:
        fail(f"L'étape courante est {current.ident}, pas {ident}.")
    if waiting:
        if status not in {"EN_COURS", "A_FAIRE"}:
            fail(f"Statut {status} : impossible de passer en attente de recette.")
        write_next_row("Statut", "`EN_ATTENTE_DE_RECETTE`")
        journal("Luna", f"`{ident}` attend une recette de l'utilisateur")
    else:
        if status != "EN_ATTENTE_DE_RECETTE":
            fail(f"Statut {status} : resume ne s'applique qu'à EN_ATTENTE_DE_RECETTE.")
        write_next_row("Statut", "`EN_COURS`")
        journal("Luna", f"`{ident}` reprise sur résultats de recette transmis par l'utilisateur")
    write_next_row("Dernière mise à jour", f"{today()} — Luna")
    print(f"✔ {ident} : statut {'EN_ATTENTE_DE_RECETTE' if waiting else 'EN_COURS'}.")


def cmd_precommit() -> None:
    _, _, status = state()
    if status != "EN_ATTENTE_DE_REVUE":
        return
    staged = subprocess.run(["git", "diff", "--cached", "--name-only"], cwd=ROOT, capture_output=True,
                            text=True, check=True).stdout.split()
    forbidden = [p for p in staged if not p.startswith(ALLOWED_WHILE_LOCKED)]
    if forbidden:
        fail("Revue en attente : commit refusé pour " + ", ".join(forbidden)
             + ". Seuls docs/, NEXT.md et la mémoire peuvent changer pendant une revue.", 3)


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return
    command, rest = args[0], args[1:]
    who = "Luna"
    if "--by" in rest:
        who = rest[rest.index("--by") + 1]
    handlers = {
        "status": lambda: cmd_status(),
        "check": lambda: cmd_check(),
        "lint": lambda: cmd_lint(),
        "precommit": lambda: cmd_precommit(),
        "wait-recette": lambda: cmd_set_wait(rest[0], True),
        "resume": lambda: cmd_set_wait(rest[0], False),
        "done": lambda: cmd_done(rest[0], who),
        "request": lambda: cmd_request(rest[0]),
        "approve": lambda: cmd_approve(rest[0]),
        "reject": lambda: cmd_reject(rest[0]),
    }
    if command not in handlers:
        fail(f"Commande inconnue : {command}")
    if command in {"done", "request", "approve", "reject", "wait-recette", "resume"} and not rest:
        fail(f"« {command} » attend un identifiant d'étape.")
    handlers[command]()


if __name__ == "__main__":
    main()
