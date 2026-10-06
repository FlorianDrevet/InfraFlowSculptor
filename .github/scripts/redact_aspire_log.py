#!/usr/bin/env python3
"""Redact credentials and tokens from Aspire CLI logs before artifact upload."""

from __future__ import annotations

import base64
import html
import json
import os
import re
import sys
from pathlib import Path
from urllib.parse import quote, quote_plus


SENSITIVE_KEY_PATTERN = (
    r"(?:[A-Za-z0-9_.-]*(?:password|passwd|pwd|secret|token|authorization)[A-Za-z0-9_.-]*"
    r"|[A-Za-z0-9_.-]*session(?:[_-]?(?:state|id|token|cookie|code))?"
    r"|[A-Za-z0-9_.-]*key(?=$|[^A-Za-z0-9])|(?<![A-Za-z0-9_.-])(?:session[_-]?state|auth[_-]?code|set[_-]?cookie|"
    r"session[_-]?code|code[_-]?verifier|cookie|code|state|nonce|csrf)(?![A-Za-z0-9_.-]))"
)
CLI_SENSITIVE_PATTERN = r"password|passwd|pwd|secret|token|key(?=$|[^A-Za-z0-9])|authorization|cookie|session|state|code|nonce|csrf"
KNOWN_SECRET_ENVIRONMENT_VARIABLES = (
    "IFS_E2E_PASSWORD",
    "Parameters__postgres-password",
    "Parameters__keycloak-admin-password",
    "Parameters__servicebus-sql-pwd",
    "Parameters__redis-password",
    "Auth__TestSigningKey",
    "AppHost__OtlpApiKey",
    "AppHost__DashboardApiKey",
    "Gitea__Token",
    "Gitea__Password",
)

UNQUOTED_SENSITIVE_VALUE = r"""[^\s,;&"'\\{}\[\]]+"""
KEY_VALUE = re.compile(
    rf'''(?i)((?:\\*["']?)?{SENSITIVE_KEY_PATTERN}(?:\\*["']?)?[ \t]*[:=][ \t]*)("(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|{UNQUOTED_SENSITIVE_VALUE}|(?=$|[\r\n,;&}}]))'''
)
CLI_ARGUMENT = re.compile(
    rf'''(?i)(--[A-Za-z0-9_.-]*(?:{CLI_SENSITIVE_PATTERN})[A-Za-z0-9_.-]*\s+)(?:"[^"]*"|'[^']*'|\S+)'''
)
CLI_ARGUMENT_ARRAY = re.compile(
    rf'''(?i)(--[A-Za-z0-9_.-]*(?:{CLI_SENSITIVE_PATTERN})[A-Za-z0-9_.-]*["']\s*,\s*["'])([^"']*)(["'])'''
)
AUTHORIZATION_CREDENTIAL = re.compile(
    r"(?i)((?:authorization\s*:\s*)?(?:bearer|basic)\s+)[A-Za-z0-9._~+/-]+=*"
)
DASHBOARD_TOKEN = re.compile(r"""(?i)(login\?t=)[^\s&"']+""")
OAUTH_PARAMETER = re.compile(
    r'''(?i)((?:[?&]|\b)(?:session[_-]?code|code[_-]?verifier|code|state|nonce|session(?:[_-]state|[_-]id)?|sid)=)[^&\s"']+'''
)
COOKIE_HEADER = re.compile(r"(?im)(^|\s)((?:set-cookie|cookie)\s*:\s*)[^\r\n]*")
COMPOSITE_VALUE_START = re.compile(
    r'''(?i)((?:\\*["']?)?'''
    + SENSITIVE_KEY_PATTERN
    + r'''(?:\\*["']?)?[ \t]*[:=][ \t]*)(?P<opening>\{|\[)'''
)
NAME_VALUE_SECRET = re.compile(
    r'''(?is)((?:\\*["']?)(?:name|key)(?:\\*["']?\s*:\s*\\*["'])[^"'\r\n]*(?:password|secret|token|key|authorization|cookie|session|state|code|nonce|csrf)[^"'\r\n]*(?:\\*["']\s*,\s*\\*["']?value\\*["']?\s*:\s*\\*["']))([^"'\r\n]*)(\\*["'])'''
)
UNSAFE_FIELD = re.compile(
    rf'''(?i){SENSITIVE_KEY_PATTERN}\s*\\*["']?\s*[:=]\s*(?!\s*(?:\\*["']?\[REDACTED\]|\{{\s*\}}|\[\s*\]))'''
)
UNSAFE_ARGUMENT = re.compile(
    rf'''(?i)--[A-Za-z0-9_.-]*(?:{CLI_SENSITIVE_PATTERN})[A-Za-z0-9_.-]*\s+(?!\s*\[REDACTED\])\S+'''
)
UNSAFE_ARGUMENT_ARRAY = re.compile(
    rf'''(?i)--[A-Za-z0-9_.-]*(?:{CLI_SENSITIVE_PATTERN})[A-Za-z0-9_.-]*["']\s*,\s*["'](?!\[REDACTED\])[^"']+'''
)
UNSAFE_NAME_VALUE = re.compile(
    r'''(?is)(?:\\*["']?)(?:name|key)(?:\\*["']?\s*:\s*\\*["'])[^"'\r\n]*(?:password|secret|token|key|authorization|cookie|session|state|code|nonce|csrf)[^"'\r\n]*(?:\\*["']\s*,\s*\\*["']?value\\*["']?\s*:\s*\\*["'])(?!\[REDACTED\])[^"'\r\n]+'''
)


def redact(text: str) -> str:
    secrets = configured_secret_variants()

    for secret in sorted(set(secrets), key=len, reverse=True):
        text = text.replace(secret, "[REDACTED]")

    try:
        document = json.loads(text)
    except json.JSONDecodeError:
        lines = text.splitlines(keepends=True)
        if lines and len(lines) > 1:
            sanitized_lines: list[str] = []
            parsed_any = False
            for line in lines:
                content = line.rstrip("\r\n")
                ending = line[len(content):]
                try:
                    document = json.loads(content)
                except json.JSONDecodeError:
                    sanitized_lines.append(redact_plain_text(line, secrets))
                    continue
                parsed_any = True
                sanitized, changed = redact_json_value(document, secrets)
                sanitized_lines.append(
                    json.dumps(sanitized, separators=(",", ":"), ensure_ascii=False) + ending
                    if changed
                    else line
                )
            if parsed_any:
                return "".join(sanitized_lines)
        return redact_plain_text(text, secrets)

    sanitized, changed = redact_json_value(document, secrets)
    return json.dumps(sanitized, separators=(",", ":"), ensure_ascii=False) if changed else text


def redact_json_value(value: object, secrets: list[str], field_name: str | None = None) -> tuple[object, bool]:
    if field_name and field_name.casefold() != "cookies" and is_sensitive_field(field_name):
        return redact_json_leaves(value), True

    if isinstance(value, dict):
        changed = False
        label = value.get("name", value.get("key"))
        sensitive_pair = isinstance(label, str) and is_sensitive_field(label)
        sanitized: dict[object, object] = {}
        for key, child in value.items():
            name = str(key)
            folded = name.casefold()
            if folded == "value" and sensitive_pair:
                sanitized[key] = "[REDACTED]"
                changed = True
            elif folded == "cookies" and isinstance(child, list):
                cookies = []
                for cookie in child:
                    if isinstance(cookie, dict):
                        safe_cookie = dict(cookie)
                        if "value" in safe_cookie:
                            safe_cookie["value"] = "[REDACTED]"
                            changed = True
                        else:
                            safe_cookie, cookie_changed = redact_json_value(safe_cookie, secrets)
                            changed = changed or cookie_changed
                        cookies.append(safe_cookie)
                    else:
                        safe_cookie, cookie_changed = redact_json_value(cookie, secrets)
                        cookies.append(safe_cookie)
                        changed = changed or cookie_changed
                sanitized[key] = cookies
                changed = True
            else:
                safe_child, child_changed = redact_json_value(child, secrets, name)
                sanitized[key] = safe_child
                changed = changed or child_changed
        return sanitized, changed

    if isinstance(value, list):
        sanitized_values = []
        changed = False
        index = 0
        while index < len(value):
            child = value[index]
            if (
                isinstance(child, str)
                and re.fullmatch(rf"(?i)--[A-Za-z0-9_.-]*(?:{CLI_SENSITIVE_PATTERN})[A-Za-z0-9_.-]*", child)
                and index + 1 < len(value)
                and isinstance(value[index + 1], str)
            ):
                sanitized_values.extend((child, "[REDACTED]"))
                changed = True
                index += 2
                continue
            safe_child, child_changed = redact_json_value(child, secrets)
            sanitized_values.append(safe_child)
            changed = changed or child_changed
            index += 1
        return sanitized_values, changed

    if isinstance(value, str):
        sanitized = redact_json_string(value, secrets)
        return sanitized, sanitized != value

    return value, False


def redact_json_leaves(value: object) -> object:
    if isinstance(value, dict):
        return {key: redact_json_leaves(child) for key, child in value.items()}
    if isinstance(value, list):
        return [redact_json_leaves(child) for child in value]
    return "[REDACTED]" if value is not None else None


def redact_json_string(value: str, secrets: list[str]) -> str:
    for secret in secrets:
        value = value.replace(secret, "[REDACTED]")
    try:
        document = json.loads(value)
    except json.JSONDecodeError:
        return redact_plain_text(value, secrets)
    sanitized, changed = redact_json_value(document, secrets)
    return json.dumps(sanitized, separators=(",", ":"), ensure_ascii=False) if changed else redact_plain_text(value, secrets)


def is_sensitive_field(name: str) -> bool:
    normalized = re.sub(r"[^a-z0-9]", "", name.casefold())
    return (
        any(term in normalized for term in ("password", "passwd", "pwd", "secret", "token", "authorization"))
        or normalized in {"session", "sessionstate", "sessionid", "sessiontoken", "sessioncookie", "sessioncode", "codeverifier", "cookie", "cookies", "setcookie", "code", "state", "nonce", "csrf"}
        or normalized.endswith(("session", "sessionstate", "sessionid", "sessiontoken", "sessioncookie", "key"))
    )


def redact_plain_text(text: str, secrets: list[str]) -> str:

    text = DASHBOARD_TOKEN.sub(r"\1[REDACTED]", text)
    text = OAUTH_PARAMETER.sub(r"\1[REDACTED]", text)
    text = COOKIE_HEADER.sub(r"\1\2[REDACTED]", text)
    text = AUTHORIZATION_CREDENTIAL.sub(r"\1[REDACTED]", text)
    text = NAME_VALUE_SECRET.sub(r"\1[REDACTED]\3", text)
    text = redact_composite_values(text)
    text = KEY_VALUE.sub(redact_key_value, text)
    text = CLI_ARGUMENT.sub(r"\1[REDACTED]", text)
    text = CLI_ARGUMENT_ARRAY.sub(r"\1[REDACTED]\3", text)

    if (
        UNSAFE_FIELD.search(text)
        or UNSAFE_ARGUMENT.search(text)
        or UNSAFE_ARGUMENT_ARRAY.search(text)
        or UNSAFE_NAME_VALUE.search(text)
        or any(secret in text for secret in secrets)
    ):
        raise ValueError("sensitive log pattern remains after redaction")

    return text


def redact_composite_values(text: str) -> str:
    """Replace structured values after sensitive keys while preserving their outer type."""
    cursor = 0
    while match := COMPOSITE_VALUE_START.search(text, cursor):
        start = match.start("opening")
        if text.startswith("[REDACTED]", start):
            cursor = start + len("[REDACTED]")
            continue
        end = find_composite_value_end(text, start)
        if end is None:
            cursor = match.end()
            continue

        replacement = "{}" if text[start] == "{" else "[]"
        text = text[:start] + replacement + text[end:]
        cursor = start + len(replacement)
    return text


def find_composite_value_end(text: str, start: int) -> int | None:
    """Find the end of a bracketed value without counting delimiters in strings/comments."""
    if start >= len(text) or text[start] not in "{[":
        return None

    closing = {"{": "}", "[": "]"}
    stack = [closing[text[start]]]
    quote_character: str | None = None
    escaped = False
    line_comment = False
    block_comment = False
    index = start + 1
    while index < len(text):
        character = text[index]
        next_character = text[index + 1] if index + 1 < len(text) else ""

        if line_comment:
            if character in "\r\n":
                line_comment = False
        elif block_comment:
            if character == "*" and next_character == "/":
                block_comment = False
                index += 1
        elif quote_character is not None:
            if escaped:
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == quote_character:
                quote_character = None
        elif character == "/" and next_character == "/":
            line_comment = True
            index += 1
        elif character == "/" and next_character == "*":
            block_comment = True
            index += 1
        elif character in "\"'`":
            quote_character = character
        elif character in "{[":
            stack.append(closing[character])
        elif character in "}]":
            if not stack or character != stack[-1]:
                return None
            stack.pop()
            if not stack:
                return index + 1

        index += 1

    return None


def redact_key_value(match: re.Match[str]) -> str:
    prefix, value = match.group(1, 2)
    delimiter = re.match(r"\\*[\"']", value)
    if delimiter and value.endswith(delimiter.group()):
        return f"{prefix}{delimiter.group()}[REDACTED]{delimiter.group()}"
    return f"{prefix}[REDACTED]"


def secret_variants(secret: str) -> list[str]:
    variants = {secret}
    for _ in range(2):
        variants.update(
            escaped
            for value in tuple(variants)
            for escaped in (
                json.dumps(value, ensure_ascii=False)[1:-1],
                json.dumps(value, ensure_ascii=True)[1:-1],
            )
        )

    variants.update(
        encoded
        for value in tuple(variants)
        for encoded in (
            html.escape(value, quote=True),
            quote(value, safe=""),
            quote_plus(value, safe=""),
            quote(value, safe="~!*'()-._"),
            form_urlencode(value),
            base64.b64encode(value.encode("utf-8")).decode("ascii"),
            base64.urlsafe_b64encode(value.encode("utf-8")).decode("ascii"),
        )
    )
    return sorted(variants, key=len, reverse=True)


def configured_secret_variants() -> list[str]:
    return sorted(
        {
            variant
            for name in KNOWN_SECRET_ENVIRONMENT_VARIABLES
            if (secret := os.environ.get(name))
            for variant in secret_variants(secret)
        },
        key=len,
        reverse=True,
    )


def form_urlencode(value: str) -> str:
    """Encode a value with the browser's application/x-www-form-urlencoded rules."""
    safe = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789*-._"
    encoded: list[str] = []
    for byte in value.encode("utf-8"):
        if byte == 0x20:
            encoded.append("+")
        elif byte in safe:
            encoded.append(chr(byte))
        else:
            encoded.append(f"%{byte:02X}")
    return "".join(encoded)


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: redact_aspire_log.py <log-file>", file=sys.stderr)
        return 2
    try:
        contents = Path(sys.argv[1]).read_text(encoding="utf-8", errors="replace")
        sys.stdout.write(redact(contents))
    except (OSError, ValueError) as exc:
        print(f"Aspire log redaction failed: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
