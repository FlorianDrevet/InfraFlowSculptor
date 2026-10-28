#!/usr/bin/env python3
"""Inject the repository E2E secret into the disposable CI Keycloak realm."""

from __future__ import annotations

import json
import os
from pathlib import Path


E2E_USERNAMES = (
    "alice@contoso.example",
    "bob@contoso.example",
    "chloe@contoso.example",
    "david@fabrikam.example",
    "emma@outlook.example",
    "nina@contoso.example",
)


def set_e2e_user_passwords(realm: dict[str, object], password: str) -> None:
    users = realm.get("users")
    if not isinstance(users, list):
        raise SystemExit("Expected E2E accounts in the CI realm.")

    for username in E2E_USERNAMES:
        matching_users = [
            user
            for user in users
            if isinstance(user, dict) and user.get("username") == username
        ]
        if len(matching_users) != 1:
            raise SystemExit(f"Expected exactly one E2E account for {username}.")

        credentials = matching_users[0].get("credentials")
        if not isinstance(credentials, list):
            raise SystemExit(f"Expected password credentials for {username}.")
        password_credentials = [
            credential
            for credential in credentials
            if isinstance(credential, dict) and credential.get("type") == "password"
        ]
        if len(password_credentials) != 1:
            raise SystemExit(f"Expected exactly one password credential for {username}.")

        password_credentials[0]["value"] = password


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

    if not isinstance(realm, dict):
        raise SystemExit("Expected a JSON object in the CI realm.")
    set_e2e_user_passwords(realm, password)
    realm_path.write_text(
        json.dumps(realm, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print("Configured the disposable CI Keycloak realm for authenticated E2E tests.")


if __name__ == "__main__":
    main()
