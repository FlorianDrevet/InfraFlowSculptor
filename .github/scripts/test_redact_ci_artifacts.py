import base64
import contextlib
import io
import json
import os
import re
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

import redact_aspire_log
import redact_playwright_traces
from redact_aspire_log import redact, secret_variants
from redact_playwright_traces import redact_archive, redact_text_artifact


class RedactAspireArtifactsTests(unittest.TestCase):
    def test_redacts_log_formats_and_encoded_secret_variants(self) -> None:
        password = 'complex" café\\path'
        environment = patch.dict(
            os.environ,
            {
                "IFS_E2E_PASSWORD": password,
                "Parameters__postgres-password": "db-secret",
                "Parameters__keycloak-admin-password": "idp-secret",
            },
        )
        environment.start()
        self.addCleanup(environment.stop)

        sample = "\n".join(
            [
                "password=raw-secret",
                "password=",
                'password: "multi word log secret"',
                "password: 'single quoted log secret'",
                '"password": ""',
                json.dumps({"password": "quoted-secret"}),
                json.dumps({"message": json.dumps({"password": password})}),
                "--token cli-secret",
                json.dumps(["--token", "array-secret"]),
                "Authorization: Bearer bearer-secret",
                "Authorization: Basic basic-secret",
                "Authorization: Basic " + base64.b64encode(b"test-user:basic-password").decode(),
                "Cookie: KEYCLOAK_SESSION=cookie-secret; another=hidden-cookie",
                "Set-Cookie: AUTH_SESSION_ID=set-cookie-secret; Path=/",
                "https://host/callback?code=oauth-code&session_state=state-secret&state=csrf-secret",
                json.dumps({"access_token": "access-secret", "id_token": "id-secret", "refresh_token": "refresh-secret"}),
                json.dumps({"name": "Cookie", "value": "cookie-pair-secret"}),
                "Pwd=pwd-secret",
                json.dumps({"name": "SERVICE_PASSWORD", "value": "paired-secret"}),
                "https://host/login?t=dashboard-secret",
                password,
                json.dumps(password),
                base64.b64encode(password.encode()).decode(),
                "db-secret",
                "idp-secret",
                "theme=dark",
            ]
        )

        sanitized = redact(sample)

        for value in (
            "raw-secret",
            "multi word log secret",
            "single quoted log secret",
            "quoted-secret",
            "cli-secret",
            "array-secret",
            "bearer-secret",
            "basic-secret",
            "basic-password",
            "cookie-secret",
            "another=hidden-cookie",
            "set-cookie-secret",
            "oauth-code",
            "state-secret",
            "csrf-secret",
            "access-secret",
            "id-secret",
            "refresh-secret",
            "cookie-pair-secret",
            "pwd-secret",
            "paired-secret",
            "dashboard-secret",
            "db-secret",
            "idp-secret",
            *secret_variants(password),
        ):
            self.assertNotIn(value, sanitized)
        self.assertIn("theme=dark", sanitized)

    def test_unknown_nested_escaped_password_field_is_redacted_structurally(self) -> None:
        nested = json.dumps({"message": json.dumps({"password": "unknown-nested-secret"})})

        with patch.dict(os.environ, {}, clear=True):
            sanitized = redact_aspire_log.redact(nested)

        self.assertEqual(json.loads(json.loads(sanitized)["message"])["password"], "[REDACTED]")

    def test_unknown_sensitive_field_fails_closed(self) -> None:
        with patch.dict(os.environ, {}, clear=True):
            with patch.object(redact_aspire_log, "KEY_VALUE", re.compile(r"(?!)()()")):
                with self.assertRaises(ValueError):
                    redact_aspire_log.redact("password=unknown-secret")

    def test_aspire_log_entrypoint_redacts_and_returns_success(self) -> None:
        password = "aspire-entrypoint-secret"
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / "aspire.log"
            log.write_text(f"password={password}", encoding="utf-8")
            output = io.StringIO()
            with patch.dict(os.environ, {"IFS_E2E_PASSWORD": password}):
                with patch.object(sys, "argv", ["redact_aspire_log.py", str(log)]):
                    with contextlib.redirect_stdout(output):
                        self.assertEqual(redact_aspire_log.main(), 0)
            self.assertNotIn(password, output.getvalue())
            self.assertIn("[REDACTED]", output.getvalue())

    def test_aspire_log_entrypoint_fails_closed_with_nonzero_status(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            log = Path(directory) / "aspire.log"
            log.write_text("password=unknown-secret", encoding="utf-8")
            errors = io.StringIO()
            with patch.dict(os.environ, {}, clear=True):
                with patch.object(redact_aspire_log, "KEY_VALUE", re.compile(r"(?!)()()")):
                    with patch.object(sys, "argv", ["redact_aspire_log.py", str(log)]):
                        with contextlib.redirect_stderr(errors):
                            self.assertEqual(redact_aspire_log.main(), 2)
            self.assertNotIn("unknown-secret", errors.getvalue())

    def test_secret_variants_include_browser_url_encoding(self) -> None:
        variants = secret_variants("star*tilde~ café")

        self.assertIn("star*tilde%7E+caf%C3%A9", variants)
        self.assertIn("star*tilde~%20caf%C3%A9", variants)

    def test_redacts_additional_known_apphost_secrets(self) -> None:
        secret = "auth-test-signing-key-value"
        service_bus_password = "servicebus-sql-password-value"
        redis_password = "redis-password-value"
        with patch.dict(
            os.environ,
            {
                "Auth__TestSigningKey": secret,
                "Parameters__servicebus-sql-pwd": service_bus_password,
                "Parameters__redis-password": redis_password,
            },
        ):
            sanitized = redact_aspire_log.redact(
                f"startup signingKey={secret}; bus={service_bus_password}; cache={redis_password}"
            )

        self.assertNotIn(secret, sanitized)
        self.assertNotIn(service_bus_password, sanitized)
        self.assertNotIn(redis_password, sanitized)
        self.assertIn("[REDACTED]", sanitized)

    def test_structural_json_redaction_preserves_session_storage_and_keycloak_url(self) -> None:
        sample = json.dumps(
            {
                "contextOptions": {
                    "baseURL": "https://keycloak:8080/realms/ifs",
                    "sessionStorage": [
                        {"name": "oidc.access_token", "value": "session-access-token"}
                    ],
                }
            }
        )

        sanitized = redact_aspire_log.redact(sample)
        document = json.loads(sanitized)

        self.assertEqual(document["contextOptions"]["baseURL"], "https://keycloak:8080/realms/ifs")
        self.assertEqual(document["contextOptions"]["sessionStorage"][0]["value"], "[REDACTED]")

    def test_redacts_base64_secret_at_non_aligned_offset_by_omitting_artifact(self) -> None:
        password = "credential-hidden-inside-base64"
        encoded = base64.b64encode(
            b"prefix padding bytes" + password.encode("utf-8") + b"suffix padding bytes"
        ).decode("ascii")

        with tempfile.TemporaryDirectory() as directory:
            artifact = Path(directory) / "network-report.txt"
            artifact.write_text(encoded, encoding="ascii")

            self.assertFalse(redact_playwright_traces.redact_file(artifact, secret_variants(password)))
            self.assertFalse(artifact.exists())
            self.assertEqual(len(list(Path(directory).glob("artifact-omitted-*.txt"))), 1)

    def test_redacts_credentials_from_playwright_trace_archive(self) -> None:
        password = 'trace" café\\secret'
        variants = secret_variants(password)
        payload = (
            "typed="
            + password
            + " json="
            + json.dumps(password)
            + " base64="
            + base64.b64encode(password.encode()).decode()
        ).encode()

        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / "report-attachment.zip"
            with zipfile.ZipFile(archive, "w") as trace:
                trace.writestr("trace.trace", payload)

            self.assertTrue(redact_archive(archive, variants))

            with zipfile.ZipFile(archive) as trace:
                sanitized = trace.read("trace.trace")
            for value in variants:
                self.assertNotIn(value.encode(), sanitized)
            self.assertIn(b"[REDACTED]", sanitized)

    def test_redacts_text_reports_and_embedded_zip_attachments(self) -> None:
        password = "report-secret"
        variants = secret_variants(password)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            archive = root / "attachment.zip"
            with zipfile.ZipFile(archive, "w") as report_data:
                report_data.writestr("report.json", json.dumps({"error": password}))
            encoded_archive = base64.b64encode(archive.read_bytes()).decode("ascii")
            html_report = root / "index.html"
            html_report.write_text(
                f'<script>const data="data:application/zip;base64,{encoded_archive}";</script><p>{password}</p>',
                encoding="utf-8",
            )
            junit_report = root / "junit.xml"
            junit_report.write_text(f"<failure>password={password}</failure>", encoding="utf-8")

            self.assertTrue(redact_text_artifact(html_report, variants))
            self.assertTrue(redact_text_artifact(junit_report, variants))

            html_text = html_report.read_text(encoding="utf-8")
            self.assertNotIn(password, html_text)
            embedded = html_text.split("data:application/zip;base64,", maxsplit=1)[1].split('"', maxsplit=1)[0]
            with zipfile.ZipFile(io.BytesIO(base64.b64decode(embedded))) as report_data:
                report_json = report_data.read("report.json")
            self.assertNotIn(password.encode(), report_json)
            self.assertNotIn(password, junit_report.read_text(encoding="utf-8"))

    def test_omits_trace_when_archive_cannot_be_redacted(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / "trace.zip"
            archive.write_bytes(b"not a zip archive")

            self.assertFalse(redact_archive(archive, ["test-secret"]))
            self.assertFalse(archive.exists())
            self.assertEqual(len(list(Path(directory).glob("artifact-omitted-*.txt"))), 1)

    def test_redacts_nested_trace_archives(self) -> None:
        password = "nested-trace-secret"
        variants = secret_variants(password)
        nested = io.BytesIO()
        with zipfile.ZipFile(nested, "w") as trace:
            trace.writestr("trace.trace", f"typed={password}".encode())
        outer = io.BytesIO()
        with zipfile.ZipFile(outer, "w") as report:
            report.writestr("nested/trace.zip", nested.getvalue())

        sanitized = redact_playwright_traces.redact_archive_bytes(outer.getvalue(), variants)

        with zipfile.ZipFile(io.BytesIO(sanitized)) as report:
            nested_bytes = report.read("nested/trace.zip")
        with zipfile.ZipFile(io.BytesIO(nested_bytes)) as trace:
            trace_text = trace.read("trace.trace").decode()
        self.assertNotIn(password, trace_text)
        self.assertIn("[REDACTED]", trace_text)

    def test_redacts_unknown_text_extension_and_omits_binary_with_secret(self) -> None:
        password = "capture-secret"
        variants = secret_variants(password)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            har = root / "network.har"
            har.write_text(f'{{"request":{{"postData":"{password}"}}}}', encoding="utf-8")
            image = root / "capture.png"
            image.write_bytes(b"\x89PNG\r\n" + password.encode())
            auth_image = root / "auth-capture.png"
            auth_image.write_bytes(b"\x89PNG\r\nAuthorization: Bearer trace-token")

            self.assertTrue(redact_playwright_traces.redact_file(har, variants))
            self.assertNotIn(password, har.read_text(encoding="utf-8"))
            self.assertFalse(redact_playwright_traces.redact_file(image, variants))
            self.assertFalse(redact_playwright_traces.redact_file(auth_image, variants))
            self.assertFalse(image.exists())
            self.assertFalse(auth_image.exists())
            self.assertEqual(len(list(root.glob("artifact-omitted-*.txt"))), 2)

    def test_redacts_unrecognized_form_and_json_password_values(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            report = root / "unknown-format.txt"
            report.write_text(
                'request password=unmatched%7Esecret&scope=openid '
                'json={"password":"unmatched value with spaces"}',
                encoding="utf-8",
            )

            self.assertTrue(redact_playwright_traces.redact_file(report, ["configured-secret"]))

            sanitized = report.read_text(encoding="utf-8")
            self.assertNotIn("unmatched%7Esecret", sanitized)
            self.assertNotIn("unmatched value with spaces", sanitized)
            self.assertIn("password=[REDACTED]", sanitized)
            self.assertIn('"password":"[REDACTED]"', sanitized)

    def test_redacts_oauth_tokens_and_cookie_headers_from_playwright_text(self) -> None:
        basic = base64.b64encode(b"test-user:basic-password").decode()
        sample = "\n".join(
            [
                "https://host/callback?code=auth-code&state=state-token&session_state=session-state",
                "https://host/callback?session_code=one-time-session-code",
                "POST /protocol/openid-connect/token code_verifier=pkce-verifier-token",
                "Authorization: Bearer bearer-token",
                f"Authorization: Basic {basic}",
                "Cookie: KEYCLOAK_SESSION=session-cookie; another=hidden-cookie",
                "Set-Cookie: AUTH_SESSION_ID=set-cookie-token; Path=/",
                "  Set-Cookie: AUTH_SESSION_ID=indented-cookie; tenant=indented-header-cookie-token",
                "response headers - Cookie: KEYCLOAK_SESSION=first; another=prefix-cookie-token",
                json.dumps(
                    {
                        "access_token": "access-token",
                        "id_token": "id-token",
                        "refresh_token": "refresh-token",
                        "name": "Cookie",
                        "value": "cookie-pair-token",
                    }
                ),
            ]
        )
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "network.har"
            report.write_text(sample, encoding="utf-8")

            self.assertTrue(redact_playwright_traces.redact_file(report, ["configured-secret"]))

            sanitized = report.read_text(encoding="utf-8")
            for value in (
                "auth-code",
                "state-token",
                "session-state",
                "bearer-token",
                basic,
                "session-cookie",
                "hidden-cookie",
                "set-cookie-token",
                "indented-cookie",
                "indented-header-cookie-token",
                "prefix-cookie-token",
                "one-time-session-code",
                "pkce-verifier-token",
                "access-token",
                "id-token",
                "refresh-token",
                "cookie-pair-token",
            ):
                self.assertNotIn(value, sanitized)

    def test_redacts_keycloak_cookies_in_trace_style_network_json(self) -> None:
        records = [
            {
            "type": "request",
                "request": {
                    "cookies": [
                        {"name": "KEYCLOAK_IDENTITY", "value": "identity-jwt-secret"},
                        {"name": "KC_RESTART", "value": "restart-cookie-secret"},
                    ]
                },
            },
            {
                "type": "response",
                "response": {
                    "cookies": [{"name": "KEYCLOAK_IDENTITY", "value": "response-jwt-secret"}]
                },
            },
        ]
        network = "\n".join(json.dumps(record) for record in records) + "\n"
        with tempfile.TemporaryDirectory() as directory:
            trace = Path(directory) / "trace.network"
            trace.write_text(network, encoding="utf-8")

            self.assertTrue(redact_playwright_traces.redact_file(trace, ["configured-secret"]))

            sanitized_records = [json.loads(line) for line in trace.read_text(encoding="utf-8").splitlines()]
        cookie_values = [
            cookie["value"]
            for record in sanitized_records
            for section in (record.get("request", {}), record.get("response", {}))
            for cookie in section.get("cookies", [])
        ]
        self.assertEqual(cookie_values, ["[REDACTED]", "[REDACTED]", "[REDACTED]"])

    def test_realistic_playwright_trace_archive_stays_valid_after_redaction(self) -> None:
        access_token = "playwright-storage-access-token"
        network_token = "playwright-network-bearer-token"
        identity_cookie = "playwright-keycloak-identity-cookie"
        trace_lines = [
            json.dumps(
                {
                    "type": "context-options",
                    "contextOptions": {
                        "baseURL": "https://keycloak:8080/realms/ifs",
                        "storageState": {
                            "cookies": [{"name": "KEYCLOAK_IDENTITY", "value": identity_cookie}],
                            "origins": [
                                {
                                    "origin": "http://localhost:4200",
                                    "localStorage": [{"name": "oidc.access_token", "value": access_token}],
                                }
                            ],
                        },
                        "sessionStorage": [{"name": "theme", "value": "dark"}],
                    },
                }
            ),
            json.dumps({"type": "before", "callId": "call@1", "apiName": "page.goto"}),
        ]
        network_lines = [
            json.dumps(
                {
                    "type": "request",
                    "request": {
                        "url": "https://localhost:8080/realms/ifs",
                        "headers": [{"name": "Authorization", "value": f"Bearer {network_token}"}],
                        "cookies": [{"name": "KEYCLOAK_IDENTITY", "value": identity_cookie}],
                    },
                }
            )
        ]
        set_cookie_header = "KEYCLOAK_IDENTITY=response-header-cookie-secret"
        network_lines.append(
            json.dumps(
                {
                    "type": "response",
                    "response": {
                        "headers": [{"name": "Set-Cookie", "value": set_cookie_header}],
                    },
                }
            )
        )

        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / "trace.zip"
            with zipfile.ZipFile(archive, "w") as trace:
                trace.writestr("trace.trace", "\n".join(trace_lines) + "\n", compress_type=zipfile.ZIP_STORED)
                trace.writestr("trace.network", "\n".join(network_lines) + "\n", compress_type=zipfile.ZIP_DEFLATED)
                trace.writestr("resources/", b"")

            self.assertTrue(redact_archive(archive, secret_variants(access_token) + secret_variants(network_token) + secret_variants(identity_cookie)))

            with zipfile.ZipFile(archive) as trace:
                self.assertIsNone(trace.testzip())
                self.assertEqual(trace.getinfo("trace.trace").compress_type, zipfile.ZIP_STORED)
                self.assertEqual(trace.getinfo("trace.network").compress_type, zipfile.ZIP_DEFLATED)
                self.assertTrue(trace.getinfo("resources/").is_dir())
                trace_events = [json.loads(line) for line in trace.read("trace.trace").decode().splitlines()]
                network_events = [json.loads(line) for line in trace.read("trace.network").decode().splitlines()]

        context_options = trace_events[0]["contextOptions"]
        self.assertEqual(context_options["baseURL"], "https://keycloak:8080/realms/ifs")
        self.assertEqual(context_options["sessionStorage"][0]["value"], "dark")
        self.assertEqual(context_options["storageState"]["cookies"][0]["value"], "[REDACTED]")
        self.assertEqual(context_options["storageState"]["origins"][0]["localStorage"][0]["value"], "[REDACTED]")
        request = network_events[0]["request"]
        self.assertEqual(request["headers"][0]["value"], "[REDACTED]")
        self.assertEqual(request["cookies"][0]["value"], "[REDACTED]")
        self.assertEqual(network_events[1]["response"]["headers"][0]["value"], "[REDACTED]")

    def test_minified_text_report_redacts_object_and_array_credential_values(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "index.txt"
            report.write_text(
                'window.report={key:{nested:"private-value"},state:[{code:"auth-code"}]};',
                encoding="utf-8",
            )

            self.assertTrue(redact_playwright_traces.redact_file(report, ["configured-secret"]))

            sanitized = report.read_text(encoding="utf-8")
            self.assertNotIn("private-value", sanitized)
            self.assertNotIn("auth-code", sanitized)
            self.assertIn("window.report={key:{},state:[]};", sanitized)

    def test_playwright_html_preserves_minified_javascript_and_redacts_known_secret(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "index.html"
            report.write_text(
                '<script type="module">const e=1;const report={state:e,key:{value:"html-secret"}};</script>'
                '<p>html-secret</p>',
                encoding="utf-8",
            )

            self.assertTrue(redact_playwright_traces.redact_file(report, ["html-secret"]))

            sanitized = report.read_text(encoding="utf-8")
            self.assertNotIn("html-secret", sanitized)
            self.assertIn('const report={state:e,key:{value:"[REDACTED]"}};', sanitized)
            self.assertIn('<p>[REDACTED]</p>', sanitized)

    def test_nested_escaped_json_password_is_redacted_without_corruption(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "trace.trace"
            report.write_text(
                json.dumps({"message": json.dumps({"password": "unknown-nested-secret"})}),
                encoding="utf-8",
            )

            self.assertTrue(redact_playwright_traces.redact_file(report, ["configured-secret"]))
            outer = json.loads(report.read_text(encoding="utf-8"))
            self.assertEqual(json.loads(outer["message"])["password"], "[REDACTED]")

    def test_omits_archive_with_secret_metadata_and_redacts_entry_names(self) -> None:
        password = "metadata-secret"
        variants = secret_variants(password)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            archive = root / "entry-name.zip"
            with zipfile.ZipFile(archive, "w") as trace:
                trace.writestr(f"trace-{password}.trace", b"safe content")

            self.assertTrue(redact_archive(archive, variants))
            with zipfile.ZipFile(archive) as trace:
                self.assertEqual(trace.namelist(), ["trace-[REDACTED].trace"])

            metadata = root / "metadata.zip"
            info = zipfile.ZipInfo("trace.trace")
            info.comment = password.encode()
            with zipfile.ZipFile(metadata, "w") as trace:
                trace.writestr(info, b"safe content")

            self.assertFalse(redact_archive(metadata, variants))
            self.assertFalse(metadata.exists())

    def test_embedded_zip_inside_archive_entry_is_redacted(self) -> None:
        password = "deep-embedded-secret"
        variants = secret_variants(password)
        embedded = io.BytesIO()
        with zipfile.ZipFile(embedded, "w") as report:
            report.writestr("report.json", json.dumps({"password": password}))
        html = (
            '<script type="module">const e=1;const report={state:e,password:"'
            + password
            + '"};</script><p>'
            + password
            + "</p>data:application/zip;base64,"
            + base64.b64encode(embedded.getvalue()).decode("ascii")
        )
        outer = io.BytesIO()
        with zipfile.ZipFile(outer, "w") as trace:
            trace.writestr("attachments/index.html", html)

        sanitized = redact_playwright_traces.redact_archive_bytes(outer.getvalue(), variants)

        with zipfile.ZipFile(io.BytesIO(sanitized)) as trace:
            sanitized_html = trace.read("attachments/index.html").decode()
        self.assertIn("const report={state:e,password:", sanitized_html)
        self.assertIn("state:e", sanitized_html)
        self.assertNotIn(password, sanitized_html)
        encoded = sanitized_html.split("data:application/zip;base64,", maxsplit=1)[1]
        with zipfile.ZipFile(io.BytesIO(base64.b64decode(encoded))) as report:
            report_text = report.read("report.json").decode()
        self.assertNotIn(password, report_text)

    def test_archive_nesting_limit_fails_closed(self) -> None:
        nested = b"payload"
        for _ in range(redact_playwright_traces.MAX_ARCHIVE_DEPTH + 2):
            archive = io.BytesIO()
            with zipfile.ZipFile(archive, "w") as trace:
                trace.writestr("nested.zip", nested)
            nested = archive.getvalue()

        with self.assertRaises(ValueError):
            redact_playwright_traces.redact_archive_bytes(nested, ["configured-secret"])

    def test_entrypoint_scans_all_artifact_roots_without_any_zip_files(self) -> None:
        password = "text-only-secret"
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            report_dir = root / "report"
            results_dir = root / "results"
            captures_dir = root / "captures"
            report_dir.mkdir()
            results_dir.mkdir()
            captures_dir.mkdir()
            report = report_dir / "error-context.md"
            postgres_password = "ci-postgres-sensitive-value"
            keycloak_password = "ci-keycloak-sensitive-value"
            service_bus_password = "ci-servicebus-sensitive-value"
            redis_password = "ci-redis-sensitive-value"
            report.write_text(
                f"Playwright failed after entering {password}; database={postgres_password}; bus={service_bus_password}; cache={redis_password}",
                encoding="utf-8",
            )
            junit = results_dir / "junit.xml"
            junit.write_text(f"<failure>{keycloak_password}</failure>", encoding="utf-8")
            capture = captures_dir / "capture.har"
            capture.write_text(f"request={password}", encoding="utf-8")
            with patch.dict(
                os.environ,
                {
                    "IFS_E2E_PASSWORD": password,
                    "Parameters__postgres-password": postgres_password,
                    "Parameters__keycloak-admin-password": keycloak_password,
                    "Parameters__servicebus-sql-pwd": service_bus_password,
                    "Parameters__redis-password": redis_password,
                },
            ):
                with patch.object(
                    sys,
                    "argv",
                    ["redact_playwright_traces.py", str(report_dir), str(results_dir), str(captures_dir)],
                ):
                    self.assertEqual(redact_playwright_traces.main(), 0)
            for artifact in (report, junit, capture):
                self.assertNotIn(password, artifact.read_text(encoding="utf-8"))
            self.assertNotIn(postgres_password, report.read_text(encoding="utf-8"))
            self.assertNotIn(keycloak_password, junit.read_text(encoding="utf-8"))
            self.assertNotIn(service_bus_password, report.read_text(encoding="utf-8"))
            self.assertNotIn(redis_password, report.read_text(encoding="utf-8"))

    def test_entrypoint_omits_text_reports_without_a_secret(self) -> None:
        password = "text-only-secret"
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "junit.xml"
            report.write_text(f"<failure>{password}</failure>", encoding="utf-8")
            with patch.dict(os.environ, {}, clear=True):
                with patch.object(sys, "argv", ["redact_playwright_traces.py", directory]):
                    self.assertEqual(redact_playwright_traces.main(), 0)
            self.assertFalse(report.exists())
            self.assertEqual(len(list(Path(directory).glob("artifact-omitted-*.txt"))), 1)


if __name__ == "__main__":
    unittest.main()
