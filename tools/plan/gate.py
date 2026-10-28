"""Garde-fou du plan d'implémentation : étape courante, verrous de revue, avancement.

Le plan est la suite des titres d'étapes des fichiers `docs/plan/NN-*.md`, dans l'ordre des noms de
fichiers puis du texte :

    ### S-03 — Titre d'une étape
    ### 🔒 R-01 — Titre d'un verrou de revue

`NEXT.md` (racine) porte l'étape courante et son statut dans son tableau « En un coup d'œil ».
Un verrou `R-nn` est levé par l'auto-validation Codex dans
`docs/plan/revues/R-nn-autovalidation.md`. Les revues Claude historiques restent reconnues.

Commandes (toutes en Python 3.11+, sans dépendance) :

  python tools/plan/gate.py status            état courant, prochaine étape, verrous
  python tools/plan/gate.py check             échoue (code 3) si un vrai blocage empêche le travail
  python tools/plan/gate.py lint              vérifie la forme du plan (identifiants, rubriques)
  python tools/plan/gate.py done <ID>         Codex : l'étape <ID> est terminée → passe à la suivante
  python tools/plan/gate.py auto-review <R-nn> Codex : valide un verrou avec le rapport d'auto-validation
  python tools/plan/gate.py block <ID> <preuve> Codex : bloque une étape après vérification d'un échec
  python tools/plan/gate.py unblock <ID> <preuve> Codex : lève un blocage après vérification de sa résolution
  python tools/plan/gate.py precommit         hook git : conserve la compatibilité, sans verrou de revue humaine
  python tools/plan/gate.py wait-recette <ID>  Codex : marque une recette autonome à exécuter
  python tools/plan/gate.py resume <ID>        Codex : reprend après exécution vérifiée de la recette

Statuts de NEXT.md : A_FAIRE, EN_COURS, EN_ATTENTE_DE_REVUE, EN_ATTENTE_DE_RECETTE, CORRECTIONS_DEMANDEES, BLOQUE.

Un fichier de plan qui contient la ligne « > **Niveau : découpé.** » n'est pas exécutable : `check` refuse toute
étape qu'il contient (code 3) tant que le détail d'exécution n'est pas suffisant. Si les références ne permettent pas de le
préciser sans choix de conception, Codex demande ce choix à Florian.
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
REQUIRED_MARKERS = ("🔧", "✅", "🧪")
AUTO_REVIEW_SECTIONS = (
    "## Vérifications exécutées",
    "## Recette exécutée",
    "## Écarts",
    "## Contrôles de coût et sécurité",
)
RESOLUTION_MARKER = "Blocage résolu : CONFIRMÉ"
BLOCK_MARKER = "Blocage confirmé : CONFIRMÉ"


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


def auto_review_is_valid(lock_id: str) -> bool:
    review = REVIEW_DIR / f"{lock_id}-autovalidation.md"
    if not review.exists():
        return False
    content = review.read_text(encoding="utf-8")
    has_verdict = re.search(r"^Verdict\s*:\s*VALIDÉ\s*$", content, re.M) is not None
    return has_verdict and all(section in content for section in AUTO_REVIEW_SECTIONS)


def lock_is_validated(lock_id: str) -> bool:
    return verdict(lock_id) == "APPROUVÉ" or auto_review_is_valid(lock_id)


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
    return [s for s in steps[:index] if s.lock and not lock_is_validated(s.ident)]


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
        fail("Verrou(s) antérieur(s) sans revue historique approuvée ni auto-validation : "
             + ", ".join(s.ident for s in pending) + ". Revenir au verrou et exécuter son auto-revue.", 3)
    current = steps[index]
    if status == "EN_ATTENTE_DE_RECETTE":
        print(f"🧪 {current.ident} : recette autonome à exécuter par Codex, puis `resume {current.ident}` après vérification.")
    if NOT_DETAILED_MARK in current.file.read_text(encoding="utf-8"):
        fail(f"{current.file.name} est seulement découpé : il faut préciser le détail d'exécution avec l'utilisateur "
             f"avant de commencer {current.ident}.", 3)
    if status == "BLOQUE":
        fail(f"Statut BLOQUE : résoudre et consigner la condition, puis `unblock {current.ident} <preuve>`.", 3)
    if current.lock and status not in {"CORRECTIONS_DEMANDEES", "A_FAIRE", "EN_COURS", "EN_ATTENTE_DE_REVUE"}:
        fail(f"Statut {status} incohérent sur le verrou {current.ident}.")
    if status == "EN_ATTENTE_DE_REVUE":
        print(f"🔎 {current.ident} : auto-revue Codex à consigner, sans attente d'un réviseur externe.")
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
        fail(f"Un verrou se termine avec `auto-review {ident}` après consignation du rapport d'auto-validation.")
    if status == "EN_ATTENTE_DE_RECETTE":
        fail(f"La recette de {ident} doit être exécutée et vérifiée avant `resume {ident}` puis `done {ident}`.")
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
        print(f"⚠ L'étape suivante est un verrou : exécuter l'auto-revue, puis `auto-review {nxt.ident}`.")


def cmd_auto_review(ident: str) -> None:
    steps, index, status = state()
    current = steps[index]
    if current.ident != ident or not current.lock:
        fail(f"{ident} n'est pas le verrou courant (courant : {current.ident}).")
    if status == "BLOQUE":
        fail(f"{ident} reste BLOQUE : résoudre d'abord la condition bloquante.")
    if status == "EN_ATTENTE_DE_RECETTE":
        fail(f"Exécuter et vérifier la recette, puis `resume {ident}` avant l'auto-revue.")
    review = REVIEW_DIR / f"{ident}-autovalidation.md"
    if not auto_review_is_valid(ident):
        missing = [section for section in AUTO_REVIEW_SECTIONS
                   if not review.exists() or section not in review.read_text(encoding="utf-8")]
        fail(f"{review.relative_to(ROOT)} doit contenir « Verdict : VALIDÉ » et les rubriques : "
             + ", ".join(missing or AUTO_REVIEW_SECTIONS))
    nxt = steps[index + 1] if index + 1 < len(steps) else None
    write_next_row("Dernière étape terminée", f"[`{ident}`]({current.link}) — {current.title} (auto-validée)")
    write_next_row("Verrou", "aucun")
    if nxt:
        write_next_row("Étape courante", f"[`{nxt.ident}`]({nxt.link}) — {nxt.title}")
        after = steps[index + 2] if index + 2 < len(steps) else None
        write_next_row("Étape suivante", f"`{after.ident}` — {after.title}" if after else "— fin du plan détaillé")
    else:
        write_next_row("Étape courante", "— fin du plan détaillé")
        write_next_row("Étape suivante", "— fin du plan détaillé")
    write_next_row("Statut", "`A_FAIRE`")
    write_next_row("Dernière mise à jour", f"{today()} — Codex (auto-validation {ident})")
    journal("Codex", f"`{ident}` auto-validée", review.name)
    print(f"✔ Verrou {ident} auto-validé. Étape courante : {describe(nxt) if nxt else 'fin du plan détaillé'}")


def cmd_unblock(ident: str, evidence_path: str) -> None:
    steps, index, status = state()
    current = steps[index]
    if current.ident != ident or status != "BLOQUE":
        fail(f"`unblock` exige que {ident} soit l'étape courante au statut BLOQUE.")
    evidence = (ROOT / evidence_path).resolve()
    try:
        evidence.relative_to(ROOT.resolve())
    except ValueError:
        fail("La preuve de résolution doit rester dans le dépôt.")
    if not evidence.is_file():
        fail(f"Preuve de résolution introuvable : {evidence_path}.")
    content = evidence.read_text(encoding="utf-8")
    if RESOLUTION_MARKER not in content or "## Vérifications effectuées" not in content:
        fail(f"{evidence_path} doit confirmer la résolution et décrire les vérifications effectuées.")
    write_next_row("Statut", "`EN_COURS`")
    write_next_row("Verrou", "aucun")
    write_next_row("Dernière mise à jour", f"{today()} — Codex (blocage résolu)")
    journal("Codex", f"`{ident}` blocage résolu", evidence_path)
    print(f"✔ Blocage {ident} levé selon {evidence_path}.")


def cmd_block(ident: str, evidence_path: str) -> None:
    steps, index, status = state()
    current = steps[index]
    if current.ident != ident or status not in {"A_FAIRE", "EN_COURS"}:
        fail(f"`block` exige que {ident} soit l'étape courante au statut A_FAIRE ou EN_COURS.")
    evidence = (ROOT / evidence_path).resolve()
    try:
        evidence.relative_to(ROOT.resolve())
    except ValueError:
        fail("La preuve de blocage doit rester dans le dépôt.")
    if not evidence.is_file():
        fail(f"Preuve de blocage introuvable : {evidence_path}.")
    content = evidence.read_text(encoding="utf-8")
    if BLOCK_MARKER not in content or "## Vérifications effectuées" not in content:
        fail(f"{evidence_path} doit confirmer le blocage et décrire les vérifications effectuées.")
    write_next_row("Statut", "`BLOQUE`")
    write_next_row("Dernière mise à jour", f"{today()} — Codex (blocage confirmé)")
    journal("Codex", f"`{ident}` blocage confirmé", evidence_path)
    print(f"✔ {ident} bloquée selon {evidence_path}.")


def cmd_set_wait(ident: str, waiting: bool) -> None:
    steps, index, status = state()
    current = steps[index]
    if current.ident != ident:
        fail(f"L'étape courante est {current.ident}, pas {ident}.")
    if waiting:
        if status not in {"EN_COURS", "A_FAIRE"}:
            fail(f"Statut {status} : impossible de passer en attente de recette.")
        write_next_row("Statut", "`EN_ATTENTE_DE_RECETTE`")
        journal("Codex", f"`{ident}` recette autonome à exécuter")
    else:
        if status != "EN_ATTENTE_DE_RECETTE":
            fail(f"Statut {status} : resume ne s'applique qu'à EN_ATTENTE_DE_RECETTE.")
        write_next_row("Statut", "`EN_COURS`")
        journal("Codex", f"`{ident}` reprise après recette autonome vérifiée")
    write_next_row("Dernière mise à jour", f"{today()} — Codex")
    print(f"✔ {ident} : statut {'EN_ATTENTE_DE_RECETTE' if waiting else 'EN_COURS'}.")


def cmd_precommit() -> None:
    # Compatibilité pour les hooks déjà installés : l'auto-revue ne bloque pas les commits.
    return


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return
    command, rest = args[0], args[1:]
    who = "Codex"
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
        "auto-review": lambda: cmd_auto_review(rest[0]),
        "block": lambda: cmd_block(rest[0], rest[1]),
        "unblock": lambda: cmd_unblock(rest[0], rest[1]),
    }
    if command not in handlers:
        fail(f"Commande inconnue : {command}")
    if command in {"done", "auto-review", "block", "unblock", "wait-recette", "resume"} and not rest:
        fail(f"« {command} » attend un identifiant d'étape.")
    if command in {"block", "unblock"} and len(rest) < 2:
        fail(f"« {command} » attend un identifiant d'étape et un chemin de preuve.")
    handlers[command]()


if __name__ == "__main__":
    main()
