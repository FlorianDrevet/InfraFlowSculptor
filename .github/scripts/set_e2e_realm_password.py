#!/usr/bin/env python3
"""Inject the repository E2E secret into the disposable CI Keycloak realm."""

from __future__ import annotations

import json
import os
from pathlib import Path


def main() -> None:
    password = os.environ.get("IFS_E2E_PASSWORD", "")
    if len(password) < 32:
        raise SystemExit("IFS_E2E_PASSWORD must contain at least 32 characters.")

    repository_root = Path(__file__).resolve().parents[2]
    realm_path = (
        repository_root
        / "src/backend/InfraFlowSculptor.AppHost/Realms/ifs-realm.json"
    )
    realm = json.loads(realm_path.read_text(encoding="utf-8"))

    alice_accounts = [
        user for user in realm.get("users", [])
        if user.get("username") == "alice@contoso.example"
    ]
    if len(alice_accounts) != 1:
        raise SystemExit("Expected exactly one Alice E2E account in the CI realm.")

    password_credentials = [
        credential
        for credential in alice_accounts[0].get("credentials", [])
        if credential.get("type") == "password"
    ]
    if len(password_credentials) != 1:
        raise SystemExit("Expected exactly one Alice password credential in the CI realm.")

    password_credentials[0]["value"] = password
    realm_path.write_text(
        json.dumps(realm, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print("Configured the disposable CI Keycloak realm for authenticated E2E tests.")


if __name__ == "__main__":
    main()
