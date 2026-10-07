from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / "tools" / "dev" / "check-prereqs.ps1"
PWSH = shutil.which("pwsh")


class CheckPrerequisitesTests(unittest.TestCase):
    def setUp(self) -> None:
        if sys.platform != "win32":
            self.skipTest("Les commandes simulées utilisent des fichiers .cmd Windows.")
        if PWSH is None:
            self.skipTest("PowerShell 7 (pwsh) est nécessaire pour ce test.")
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.bin_dir = self.root / "bin"
        self.bin_dir.mkdir()
        self.versions_path = self.root / "versions.json"
        self.versions_path.write_text(
            json.dumps(
                {
                    "dotnet": "10.0.203",
                    "node": "24.15.0",
                    "aspire": "13.5.3",
                    "python": "3.11",
                    "pwsh": "7.5.0",
                    "bicep": "0.47.16",
                    "docker": "27.0.0",
                    "az": "2.90.0",
                }
            ),
            encoding="utf-8",
        )
        self.write_command("dotnet", "@echo off\necho 10.0.301\n")
        self.write_command("node", "@echo off\necho v24.19.0\n")
        self.write_command("aspire", "@echo off\necho 13.6.0\n")
        self.write_command("python", "@echo off\necho Python 3.14.2\n")
        self.write_command("git", "@echo off\necho git version 2.52.0.windows.1\n")
        self.write_command("gh", "@echo off\necho gh version 2.88.1\n")
        self.write_command(
            "az",
            "@echo off\n"
            'if "%1"=="version" echo {"azure-cli":"2.90.0"}\n'
            'if "%1"=="bicep" echo Bicep CLI version 0.47.16\n',
        )
        self.write_command(
            "docker",
            "@echo off\n"
            'if "%1"=="--version" echo Docker version 29.5.3, build test\n'
            'if "%1"=="info" echo 29.5.3\n',
        )

    def tearDown(self) -> None:
        self.temp.cleanup()

    def write_command(self, name: str, contents: str) -> None:
        (self.bin_dir / f"{name}.cmd").write_text(contents, encoding="ascii")

    def run_check(self) -> subprocess.CompletedProcess[str]:
        environment = os.environ.copy()
        environment["PATH"] = str(self.bin_dir) + os.pathsep + environment.get("PATH", "")
        return subprocess.run(
            [PWSH or "pwsh", "-NoLogo", "-NoProfile", "-File", str(SCRIPT), "-VersionsPath", str(self.versions_path)],
            cwd=ROOT,
            env=environment,
            capture_output=True,
            text=True,
            check=False,
        )

    def test_all_prerequisites_at_or_above_minimum_pass(self) -> None:
        result = self.run_check()
        output = result.stdout + result.stderr

        self.assertEqual(result.returncode, 0, output)
        self.assertIn("Outil", output)
        self.assertIn("Attendu", output)
        self.assertIn("24.19.0", output)
        self.assertIn("OK", output)

    def test_outdated_tool_fails_and_shows_install_command(self) -> None:
        self.write_command("node", "@echo off\necho v20.0.0\n")
        result = self.run_check()
        output = result.stdout + result.stderr

        self.assertEqual(result.returncode, 1, output)
        self.assertIn("Node", output)
        self.assertIn("KO", output)
        self.assertIn("OpenJS.NodeJS.LTS", output)


if __name__ == "__main__":
    unittest.main()
