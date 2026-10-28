import unittest

from set_e2e_realm_password import E2E_USERNAMES, set_e2e_user_passwords


class SetE2eRealmPasswordTests(unittest.TestCase):
    def make_realm(self):
        users = [
            {
                "username": username,
                "credentials": [{"type": "password", "value": "old-password", "temporary": False}],
            }
            for username in E2E_USERNAMES
        ]
        users.append(
            {
                "username": "ops@ifs.example",
                "credentials": [{"type": "password", "value": "ops-password", "temporary": False}],
            }
        )
        return {"users": users}

    def test_sets_the_shared_e2e_password_only_for_ui_test_users(self):
        realm = self.make_realm()

        set_e2e_user_passwords(realm, "test-e2e-password-0123456789abcdef")

        users = {user["username"]: user for user in realm["users"]}
        for username in E2E_USERNAMES:
            self.assertEqual(
                users[username]["credentials"][0]["value"],
                "test-e2e-password-0123456789abcdef",
            )
        self.assertEqual(users["ops@ifs.example"]["credentials"][0]["value"], "ops-password")

    def test_requires_every_ui_test_user_to_exist_exactly_once(self):
        realm = self.make_realm()
        realm["users"] = [user for user in realm["users"] if user["username"] != E2E_USERNAMES[-1]]

        with self.assertRaisesRegex(SystemExit, "Expected exactly one E2E account"):
            set_e2e_user_passwords(realm, "test-e2e-password-0123456789abcdef")

    def test_requires_one_password_credential_per_ui_test_user(self):
        realm = self.make_realm()
        realm["users"][0]["credentials"].append(
            {"type": "password", "value": "second-old-password", "temporary": False}
        )

        with self.assertRaisesRegex(SystemExit, "Expected exactly one password credential"):
            set_e2e_user_passwords(realm, "test-e2e-password-0123456789abcdef")


if __name__ == "__main__":
    unittest.main()
