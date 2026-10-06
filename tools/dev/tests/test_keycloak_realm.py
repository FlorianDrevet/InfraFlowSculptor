"""Checks the local Keycloak realm settings required by the web login flow."""

import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
REALM_FILE = ROOT / "src" / "backend" / "InfraFlowSculptor.AppHost" / "Realms" / "ifs-realm.json"


class KeycloakRealmTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.realm = json.loads(REALM_FILE.read_text(encoding="utf-8"))

    def test_web_client_has_optional_offline_access_scope(self) -> None:
        web_client = next(client for client in self.realm["clients"] if client["clientId"] == "ifs-web")

        self.assertIn("offline_access", web_client.get("optionalClientScopes", []))

    def test_seeded_users_can_request_offline_access_for_web(self) -> None:
        users = self.realm["users"]
        missing_role = sorted(
            user["username"]
            for user in users
            if "offline_access" not in user.get("realmRoles", [])
        )

        self.assertTrue(users, "The local realm must include its demo users.")
        self.assertEqual([], missing_role, "Seeded users must be allowed to receive offline tokens.")


if __name__ == "__main__":
    unittest.main()
