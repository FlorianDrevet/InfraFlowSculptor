#!/usr/bin/env python3
"""Remove configured test credentials from Playwright artifacts before upload."""

from __future__ import annotations

import base64
import copy
import hashlib
import io
import json
import os
import re
import sys
import tempfile
import zipfile
from pathlib import Path

from redact_aspire_log import configured_secret_variants, redact as redact_log_text


PLACEHOLDER = b"[REDACTED]"
MAX_ARCHIVE_DEPTH = 5
ZIP_DATA_URI = re.compile(r"(data:application/zip;base64,)([A-Za-z0-9+/=]+)", re.IGNORECASE)
BASE64_RUN = re.compile(r"(?<![A-Za-z0-9+/])([A-Za-z0-9+/]{64,}={0,2})(?![A-Za-z0-9+/])")
SENSITIVE_BINARY_MARKER = re.compile(
    rb"(?i)(?:authorization\s*:\s*(?:bearer|basic)\b|(?:access_token|refresh_token|id_token|"
    rb"cookie|set-cookie|session_state|(?:[?&])code|nonce|csrf)\s*[:=])"
)
COOKIE_ARRAY_MARKER = re.compile(r'''(?i)(?:["']cookies["']|\bcookies\b)\s*[:=]\s*\[''')


def omit_artifact(path: Path) -> None:
    path.unlink(missing_ok=True)
    digest = hashlib.sha256(path.name.encode("utf-8", errors="replace")).hexdigest()[:12]
    marker = path.parent / f"artifact-omitted-{digest}.txt"
    marker.write_text(
        "Artifact omitted because its contents could not be safely redacted.\n",
        encoding="utf-8",
    )


def write_atomic(path: Path, contents: bytes) -> None:
    handle = tempfile.NamedTemporaryFile(
        prefix=path.name + ".",
        suffix=".redacted.tmp",
        dir=path.parent,
        delete=False,
    )
    temporary = Path(handle.name)
    handle.close()
    try:
        temporary.write_bytes(contents)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def redact_embedded_archives(text: str, variants: list[str], depth: int) -> str:
    def replace(match: re.Match[str]) -> str:
        if depth >= MAX_ARCHIVE_DEPTH:
            raise ValueError("embedded archive nesting exceeds the limit")
        payload = base64.b64decode(match.group(2), validate=True)
        sanitized = redact_archive_bytes(payload, variants, depth + 1)
        return match.group(1) + base64.b64encode(sanitized).decode("ascii")

    return ZIP_DATA_URI.sub(replace, text)


def redact_sensitive_text(text: str, variants: list[str]) -> str:
    for value in variants:
        text = text.replace(value, "[REDACTED]")
    text = redact_log_text(text)
    text = redact_cookie_structures(text)
    if any(value in text for value in variants):
        raise ValueError("credential remains in artifact")
    if contains_credential_in_base64(text, variants):
        raise ValueError("credential remains in base64-encoded artifact content")
    return text


def redact_html_report(text: str, variants: list[str]) -> str:
    """Redact configured secrets without rewriting executable JavaScript in the report shell."""
    for value in variants:
        text = text.replace(value, "[REDACTED]")
    if any(value in text for value in variants):
        raise ValueError("credential remains in HTML report")
    if contains_credential_in_base64(text, variants):
        raise ValueError("credential remains in base64-encoded HTML report content")
    return text


def contains_credential_in_base64(text: str, variants: list[str]) -> bool:
    secret_bytes = {value.encode("utf-8") for value in variants if len(value) >= 6}
    if not secret_bytes:
        return False

    for match in BASE64_RUN.finditer(text):
        encoded = match.group(1)
        if len(encoded) > 16 * 1024 * 1024:
            raise ValueError("base64 artifact segment exceeds the safe inspection limit")
        encoded += "=" * ((4 - len(encoded) % 4) % 4)
        try:
            decoded = base64.b64decode(encoded, validate=True)
        except (ValueError, base64.binascii.Error):
            continue
        if any(secret in decoded for secret in secret_bytes):
            return True
    return False


def redact_cookie_structures(text: str) -> str:
    def redact_document(document: object) -> tuple[object, bool]:
        changed = False
        if isinstance(document, dict):
            for key, value in list(document.items()):
                if str(key).casefold() == "cookies" and isinstance(value, list):
                    safe_cookies = []
                    for cookie in value:
                        if isinstance(cookie, dict):
                            sanitized_cookie = dict(cookie)
                            if "value" in sanitized_cookie:
                                sanitized_cookie["value"] = "[REDACTED]"
                                changed = True
                            safe_cookies.append(sanitized_cookie)
                        elif isinstance(cookie, str):
                            safe_cookies.append("[REDACTED]")
                            changed = True
                        else:
                            sanitized, nested_changed = redact_document(cookie)
                            safe_cookies.append(sanitized)
                            changed = changed or nested_changed
                    document[key] = safe_cookies
                else:
                    sanitized, nested_changed = redact_document(value)
                    document[key] = sanitized
                    changed = changed or nested_changed
            return document, changed
        if isinstance(document, list):
            sanitized_values = []
            for value in document:
                sanitized, nested_changed = redact_document(value)
                sanitized_values.append(sanitized)
                changed = changed or nested_changed
            return sanitized_values, changed
        return document, False

    try:
        document = json.loads(text)
    except json.JSONDecodeError:
        lines = text.splitlines(keepends=True)
        sanitized_lines: list[str] = []
        for line in lines:
            try:
                document = json.loads(line)
            except json.JSONDecodeError:
                if COOKIE_ARRAY_MARKER.search(line):
                    raise ValueError("cookie array could not be safely parsed")
                sanitized_lines.append(line)
                continue
            sanitized, changed = redact_document(document)
            sanitized_lines.append(
                json.dumps(sanitized, separators=(",", ":"), ensure_ascii=False) + ("\n" if line.endswith("\n") else "")
                if changed
                else line
            )
        return "".join(sanitized_lines)

    sanitized, changed = redact_document(document)
    if changed:
        return json.dumps(sanitized, separators=(",", ":"), ensure_ascii=False)
    if COOKIE_ARRAY_MARKER.search(text):
        raise ValueError("cookie array was present but could not be safely redacted")
    return text


def redact_archive_bytes(contents: bytes, variants: list[str], depth: int = 0) -> bytes:
    if depth > MAX_ARCHIVE_DEPTH:
        raise ValueError("archive nesting exceeds the limit")

    byte_variants = [value.encode("utf-8") for value in variants if value]
    entries: list[tuple[zipfile.ZipInfo, bytes]] = []
    with zipfile.ZipFile(io.BytesIO(contents), "r") as source:
        comment = source.comment
        for value in byte_variants:
            comment = comment.replace(value, PLACEHOLDER)

        for info in source.infolist():
            if any(value in info.extra or value in info.comment for value in byte_variants) or (
                SENSITIVE_BINARY_MARKER.search(info.extra)
                or SENSITIVE_BINARY_MARKER.search(info.comment)
            ):
                raise ValueError("credential remains in archive metadata")

            entry = source.read(info)
            if zipfile.is_zipfile(io.BytesIO(entry)):
                entry = redact_archive_bytes(entry, variants, depth + 1)
            else:
                try:
                    text = entry.decode("utf-8")
                except UnicodeDecodeError:
                    for value in byte_variants:
                        entry = entry.replace(value, PLACEHOLDER)
                    if SENSITIVE_BINARY_MARKER.search(entry):
                        raise ValueError("sensitive authentication marker remains in binary archive entry")
                else:
                    if ZIP_DATA_URI.search(text):
                        text = redact_embedded_archives(text, variants, depth)
                    if info.filename.lower().endswith((".html", ".htm")):
                        text = redact_html_report(text, variants)
                    else:
                        text = redact_sensitive_text(text, variants)
                    entry = text.encode("utf-8")

            name = redact_sensitive_text(info.filename, variants)
            if any(value in name for value in variants):
                raise ValueError("credential remains in archive entry name")
            if any(value in entry for value in byte_variants):
                raise ValueError("credential remains in archive entry")
            sanitized_info = copy.copy(info)
            sanitized_info.filename = name
            entries.append((sanitized_info, entry))

    if any(value in comment for value in byte_variants) or SENSITIVE_BINARY_MARKER.search(comment):
        raise ValueError("credential remains in archive comment")

    output = io.BytesIO()
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED) as target:
        target.comment = comment
        for info, entry in entries:
            target.writestr(info, entry)

    sanitized = output.getvalue()
    with zipfile.ZipFile(io.BytesIO(sanitized), "r") as check:
        if check.testzip() is not None:
            raise zipfile.BadZipFile("archive integrity check failed")
    return sanitized


def redact_archive(path: Path, variants: list[str]) -> bool:
    try:
        sanitized = redact_archive_bytes(path.read_bytes(), variants)
        write_atomic(path, sanitized)
        return True
    except Exception:
        omit_artifact(path)
        return False


def redact_file(path: Path, variants: list[str]) -> bool:
    byte_variants = [value.encode("utf-8") for value in variants if value]
    try:
        if path.suffix.lower() == ".zip" or zipfile.is_zipfile(path):
            return redact_archive(path, variants)

        contents = path.read_bytes()
        try:
            text = contents.decode("utf-8")
        except UnicodeDecodeError:
            if any(value in contents for value in byte_variants) or SENSITIVE_BINARY_MARKER.search(contents):
                omit_artifact(path)
                return False
            return True

        if ZIP_DATA_URI.search(text):
            text = redact_embedded_archives(text, variants, 0)
        if path.suffix.lower() in {".html", ".htm"}:
            text = redact_html_report(text, variants)
        else:
            text = redact_sensitive_text(text, variants)
        write_atomic(path, text.encode("utf-8"))
        return True
    except Exception:
        omit_artifact(path)
        return False


def redact_text_artifact(path: Path, variants: list[str]) -> bool:
    """Compatibility wrapper used by the focused text-report tests."""
    return redact_file(path, variants)


def main() -> int:
    if len(sys.argv) < 2:
        print("usage: redact_playwright_traces.py <artifact-directory> [...]", file=sys.stderr)
        return 2

    secret = os.environ.get("IFS_E2E_PASSWORD", "")
    roots = [Path(argument) for argument in sys.argv[1:]]
    files = sorted(
        {
            artifact
            for root in roots
            if root.exists()
            for artifact in root.rglob("*")
            if artifact.is_file()
        }
    )
    if not files:
        return 0

    if not secret:
        for artifact in files:
            omit_artifact(artifact)
        print("Playwright artifacts omitted because the test secret is unavailable.", file=sys.stderr)
        return 0

    variants = configured_secret_variants()
    omitted = sum(not redact_file(artifact, variants) for artifact in files if artifact.exists())
    if omitted:
        print(f"::warning::Omitted {omitted} Playwright artifact(s) that could not be verified.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
