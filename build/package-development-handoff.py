#!/usr/bin/env python3
"""Deterministic, documentation-only V1.0-development handoff. Never a signed release."""
import argparse
import hashlib
import json
import os
import pathlib
import re
import sys
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
FILES = (
    "docs/USER_GUIDE.md",
    "docs/TROUBLESHOOTING.md",
    "docs/RECOVERY_GUIDE.md",
    "docs/FINAL_ACCEPTANCE.md",
    "docs/RELEASE_CHECKLIST.md",
    "docs/SECURITY_AUDIT.md",
    "docs/THIRD_PARTY_REVIEW.md",
    "docs/PROGRESS.md",
    "docs/TEST_MATRIX.md",
    "docs/HANDOFF.md",
    "docs/MASTER_PLAN_32_STEPS.md",
    "docs/REQUIREMENTS_MATRIX.md",
    "releases/V1.0/README.md",
    "releases/V1.0/RELEASE_NOTES.md",
    "releases/V1.0/RELEASE_SIGNOFF.md",
    "releases/V1.0/ARTIFACTS.md",
    "release/readiness.json",
    "state/steps.json",
    "state/project_state.json",
)
ARCHIVE_NAME = "v1-development-handoff.zip"
MANIFEST_NAME = "handoff-manifest.json"
STAMP = (2026, 10, 17, 0, 0, 0)


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def safe_source(name):
    if name.startswith("/") or "\\" in name or ".." in pathlib.PurePosixPath(name).parts:
        raise ValueError("Unsafe handoff source name")
    path = ROOT / name
    if not path.is_file() or path.is_symlink():
        raise ValueError("Required handoff file missing or linked: " + name)
    data = path.read_bytes()
    if len(data) > 3 * 1024 * 1024:
        raise ValueError("Unexpected large documentation file: " + name)
    return data


def check_blockers():
    ready = json.loads(safe_source("release/readiness.json"))
    if ready.get("releaseAuthorized") is not False:
        raise ValueError("Not permitted to create developer-only handoff from authorized release")
    items = ready.get("requirements")
    if not isinstance(items, list) or len(items) != 13 or any(x.get("status") != "DEFERRED" for x in items):
        raise ValueError("Development handoff must explicitly document all 13 release blockers")
    for path in ("updater/trust/release-v1.spki.base64",
                 "updater/trust/publisher-thumbprint.txt"):
        if (ROOT / path).read_text(encoding="utf-8").strip() != "UNCONFIGURED":
            raise ValueError("Development-only handoff cannot run with production signing configured")


def archive_information(src):
    return [{"path":name, "size":len(src[name]), "sha256":sha(src[name])}
            for name in sorted(src)]


def manifest(src):
    return {
        "schemaVersion":1,
        "product":"MOSEN_TELEGRAM_SCHEDULER",
        "packageType":"development-documentation-only",
        "releaseStatus":"BLOCKED",
        "releaseAuthorized":False,
        "signed":False,
        "containsInstaller":False,
        "containsCredentials":False,
        "acceptance":"NOT_APPROVED",
        "formalReleaseUrl":None,
        "sourceCommit":os.environ.get("GITHUB_SHA", "LOCAL_UNVERIFIED"),
        "files":archive_information(src),
        "limitations":[
            "No signed v1.0.0 release has been published.",
            "Unsigned 0.27.0-dev installer is a separate short-lived CI artifact.",
            "Real Windows 10/11 and native TDLib tests are not complete.",
            "Real 72-hour wall-clock run, 300 online sessions and 50 real groups not verified.",
            "Cloudflare production and third-party legal signoff not complete."
        ]
    }


def payload_bytes(data):
    return (json.dumps(data, ensure_ascii=False, sort_keys=True, indent=2)
            + "\n").encode("utf-8")


def make_zip(output):
    check_blockers()
    src = {name: safe_source(name) for name in FILES}
    info = manifest(src)
    packed = dict(src)
    packed[MANIFEST_NAME] = payload_bytes(info)
    output.mkdir(parents=True, exist_ok=True)
    dest = output / ARCHIVE_NAME
    with zipfile.ZipFile(dest, "w", compression=zipfile.ZIP_DEFLATED,
                         compresslevel=9, allowZip64=False) as z:
        for name in sorted(packed):
            entry = zipfile.ZipInfo(name, date_time=STAMP)
            entry.compress_type = zipfile.ZIP_DEFLATED
            entry.external_attr = 0o100644 << 16
            z.writestr(entry, packed[name], compress_type=zipfile.ZIP_DEFLATED,
                       compresslevel=9)
    (output / MANIFEST_NAME).write_bytes(payload_bytes(info))
    (output / (ARCHIVE_NAME + ".sha256")).write_text(
        sha(dest.read_bytes()) + "  " + ARCHIVE_NAME + "\n", encoding="ascii")
    verify(output)
    print("HANDOFF CREATED: documentation only; release BLOCKED; "
          + str(len(src)) + " source files; SHA256=" + sha(dest.read_bytes()))


def verify(output):
    dest = output / ARCHIVE_NAME
    local = json.loads((output / MANIFEST_NAME).read_text(encoding="utf-8"))
    digest = (output / (ARCHIVE_NAME + ".sha256")).read_text(encoding="ascii").split()[0]
    if digest != sha(dest.read_bytes()):
        raise ValueError("ZIP checksum mismatch")
    if local["releaseAuthorized"] or local["releaseStatus"] != "BLOCKED" or local["signed"]:
        raise ValueError("Unsafe release metadata")
    if local["packageType"] != "development-documentation-only" or local["containsInstaller"]:
        raise ValueError("Documentation handoff wrongly labelled as installer")
    with zipfile.ZipFile(dest, "r") as z:
        names = z.namelist()
        if len(names) != len(set(names)) or set(names) != set(FILES) | {MANIFEST_NAME}:
            raise ValueError("Wrong, repeated or missing handoff paths")
        if z.read(MANIFEST_NAME) != payload_bytes(local):
            raise ValueError("Embedded manifest differs from separate manifest")
        if set(f["path"] for f in local["files"]) != set(FILES):
            raise ValueError("Manifest file list mismatch")
        for file in local["files"]:
            name = file["path"]
            data = z.read(name)
            if file["sha256"] != sha(data) or file["size"] != len(data) or data != safe_source(name):
                raise ValueError("Mismatch or corrupted handoff file: " + name)
        for name in names:
            if name.startswith("/") or "\\" in name or ".." in pathlib.PurePosixPath(name).parts:
                raise ValueError("Archive path traversal")
    print("HANDOFF VERIFIED:", len(FILES), "documentation files, no EXE, no release authorization")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="artifacts/handoff")
    parser.add_argument("--verify", action="store_true")
    opts = parser.parse_args()
    try:
        target = pathlib.Path(opts.output).resolve()
        if opts.verify:
            verify(target)
        else:
            make_zip(target)
    except (OSError, ValueError, KeyError, zipfile.BadZipFile) as exc:
        print("HANDOFF BLOCKED:", str(exc), file=sys.stderr)
        sys.exit(1)
