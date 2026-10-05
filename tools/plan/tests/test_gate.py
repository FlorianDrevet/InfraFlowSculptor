from __future__ import annotations

import contextlib
import io
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from tools.plan import gate


class GateTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.plan_dir = self.root / "docs" / "plan"
        self.review_dir = self.plan_dir / "revues"
        self.review_dir.mkdir(parents=True)
        self.next_path = self.root / "NEXT.md"
        self.journal_path = self.plan_dir / "JOURNAL.md"
        self.journal_path.write_text("| Date | Acteur | Événement | Détail |\n", encoding="utf-8")
        self.write_plan()
        self.write_next()
        self.path_patches = [
            patch.object(gate, "ROOT", self.root),
            patch.object(gate, "PLAN_DIR", self.plan_dir),
            patch.object(gate, "REVIEW_DIR", self.review_dir),
            patch.object(gate, "NEXT", self.next_path),
            patch.object(gate, "JOURNAL", self.journal_path),
        ]
        for path_patch in self.path_patches:
            path_patch.start()

    def tearDown(self) -> None:
        for path_patch in reversed(self.path_patches):
            path_patch.stop()
        self.temp.cleanup()

    def write_plan(self, *, duplicate: bool = False, omit_manual_test: bool = False) -> None:
        manual_test = "" if omit_manual_test else "🧪 Test manuel.\n"
        body = (
            "### S-01 — Socle\n"
            "🎯 Objectif.\n"
            "🔧 À faire.\n"
            "✅ Vérification automatique.\n"
            f"{manual_test}\n"
            "### S-02 — Suite\n"
            "🎯 Objectif.\n"
            "🔧 À faire.\n"
            "✅ Vérification automatique.\n"
            "🧪 Test manuel.\n\n"
            "### 🔒 R-01 — Revue\n"
        )
        (self.plan_dir / "00-socle.md").write_text(body, encoding="utf-8")
        if duplicate:
            duplicate_test = "" if omit_manual_test else "🧪 Test manuel.\n"
            (self.plan_dir / "01-duplicate.md").write_text(
                "### S-01 — Doublon\n"
                "🎯 Objectif.\n"
                "🔧 À faire.\n"
                "✅ Vérification automatique.\n"
                f"{duplicate_test}\n",
                encoding="utf-8",
            )

    def write_next(self, current: str = "S-01", status: str = "A_FAIRE") -> None:
        title = "Revue" if current == "R-01" else "Socle"
        self.next_path.write_text(
            "# NEXT\n\n"
            "| | |\n|---|---|\n"
            f"| **Étape courante** | [`{current}`](docs/plan/00-socle.md#{current.lower()}--{title.lower()}) |\n"
            f"| **Statut** | `{status}` |\n"
            "| **Dernière étape terminée** | — |\n"
            "| **Étape suivante** | `S-02` — Suite |\n"
            "| **Verrou** | aucun |\n"
            "| **Dernière mise à jour** | — |\n",
            encoding="utf-8",
        )

    def test_lint_reports_duplicate_id_and_missing_manual_test(self) -> None:
        self.write_plan(duplicate=True, omit_manual_test=True)
        output = io.StringIO()
        with contextlib.redirect_stdout(output), self.assertRaises(SystemExit) as raised:
            gate.cmd_lint()

        self.assertEqual(raised.exception.code, 1)
        self.assertIn("S-01 dupliqué", output.getvalue())
        self.assertIn("rubrique 🧪 absente", output.getvalue())

    def test_done_advances_and_appends_journal(self) -> None:
        with contextlib.redirect_stdout(io.StringIO()), patch.object(gate, "head_commit", return_value="abc123"):
            gate.cmd_done("S-01", "Luna")

        next_text = self.next_path.read_text(encoding="utf-8")
        self.assertIn("[`S-02`](docs/plan/00-socle.md#s-02--suite)", next_text)
        self.assertIn("[`S-01`](docs/plan/00-socle.md#s-01--socle)", next_text)
        self.assertIn("commit `abc123`", next_text)
        self.assertIn("`S-01` terminée", self.journal_path.read_text(encoding="utf-8"))

    def test_request_requires_review_demand(self) -> None:
        self.write_next(current="R-01")
        output = io.StringIO()
        with contextlib.redirect_stdout(output), self.assertRaises(SystemExit) as raised:
            gate.cmd_request("R-01")

        self.assertEqual(raised.exception.code, 1)
        self.assertIn("R-01-demande.md", output.getvalue())

    def test_approve_requires_approved_verdict(self) -> None:
        self.write_next(current="R-01")
        (self.review_dir / "R-01-revue.md").write_text("Verdict : CORRECTIONS\n", encoding="utf-8")
        output = io.StringIO()
        with contextlib.redirect_stdout(output), self.assertRaises(SystemExit) as raised:
            gate.cmd_approve("R-01")

        self.assertEqual(raised.exception.code, 1)
        self.assertIn("Verdict : APPROUVÉ", output.getvalue())

    def test_check_returns_three_when_review_is_pending(self) -> None:
        self.write_next(current="R-01", status="EN_ATTENTE_DE_REVUE")
        output = io.StringIO()
        with contextlib.redirect_stdout(output), self.assertRaises(SystemExit) as raised:
            gate.cmd_check()

        self.assertEqual(raised.exception.code, 3)
        self.assertIn("Revue R-01 en attente", output.getvalue())

    def test_precommit_rejects_code_during_review(self) -> None:
        self.write_next(current="R-01", status="EN_ATTENTE_DE_REVUE")
        with contextlib.redirect_stdout(io.StringIO()), patch.object(
            gate.subprocess, "run", return_value=SimpleNamespace(stdout="src/x.cs\n")
        ):
            with self.assertRaises(SystemExit) as raised:
                gate.cmd_precommit()

        self.assertEqual(raised.exception.code, 3)

    def test_precommit_accepts_review_document_during_review(self) -> None:
        self.write_next(current="R-01", status="EN_ATTENTE_DE_REVUE")
        with patch.object(
            gate.subprocess,
            "run",
            return_value=SimpleNamespace(stdout="docs/plan/revues/R-01-demande.md\n"),
        ):
            gate.cmd_precommit()


if __name__ == "__main__":
    unittest.main()
