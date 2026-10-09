"""Reopen existing GPU captures with one exact, recorded Unity version-file migration."""
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(r"F:\OpenRPG_ImportWork\unity-blood-raw-mip-sampling-reference-v1")

def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def read(path):
    return json.loads(Path(path).read_text("utf-8-sig"))

def main():
    prepared_path = ROOT / "PREPARED.v1.json"
    old = read(prepared_path)
    version = ROOT / "project/ProjectSettings/ProjectVersion.txt"
    expected_before = b"m_EditorVersion: 6000.5.8f1"
    expected_after = b"m_EditorVersion: 6000.5.8f1\nm_EditorVersionWithRevision: 6000.5.8f1 (5cb7df797b7d)\n"
    if old["source_pins"][str(version)] != hashlib.sha256(expected_before).hexdigest() or version.read_bytes() != expected_after:
        raise RuntimeError("Version-file change exceeds exact known installed-editor normalization")
    native = read(ROOT / "samples/RESULT.v1.json")
    if native["unity_version"] != "6000.5.8f1" or read(ROOT / "RUN-RESULT.v1.json")["actual_unity_exit_code"] != 0:
        raise RuntimeError("Actual completed native capture absent")
    changed = [path for path, digest in old["source_pins"].items() if sha(path) != digest]
    settings = ROOT / "project/ProjectSettings/ProjectSettings.asset"
    if set(changed) != {str(version), str(settings)}:
        raise RuntimeError("Other pinned inputs changed: " + str(changed))
    import re
    settings_bytes = settings.read_bytes()
    if re.findall(rb"^  m_ActiveColorSpace: (\d+)$", settings_bytes, re.MULTILINE) != [b"0"] or native["color_space"] != "Gamma":
        raise RuntimeError("Actual Gamma isolated raw-storage context required")
    migration = {"schema": "openrpg.isolated-unity-project-version-normalization.v1", "checked_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "path": str(version), "before_sha256": old["source_pins"][str(version)], "after_sha256": sha(version), "exact_before_utf8": expected_before.decode(), "exact_after_utf8": expected_after.decode(), "old_preparation_sha256": sha(prepared_path), "all_other_pins_unchanged_except_explicit_isolated_settings": True, "settings_migration": {"path": str(settings), "before_sha256": old["source_pins"][str(settings)], "after_sha256": sha(settings), "original_settings_bytes_reconstructed": False, "captured_context": "Gamma", "scope": "explicit raw storage upload and sampling only; no original AssetDatabase or material parity"}, "original_assets_modified": False, "native_capture_rerun": False}
    with (ROOT / "VERSION-NORMALIZATION.v3.json").open("x", encoding="utf-8") as output:
        json.dump(migration, output, indent=2)
    revised = dict(old)
    revised["source_pins"] = dict(old["source_pins"])
    revised["source_pins"][str(version)] = sha(version)
    revised["source_pins"][str(settings)] = sha(settings)
    revised["source_pins"][str(prepared_path)] = sha(prepared_path)
    revised["source_pins"][str(ROOT / "VERSION-NORMALIZATION.v3.json")] = sha(ROOT / "VERSION-NORMALIZATION.v3.json")
    revised["source_pins"][str(Path(__file__))] = sha(Path(__file__))
    revised["explicit_isolated_project_version_normalization_receipt"] = str(ROOT / "VERSION-NORMALIZATION.v3.json")
    with (ROOT / "PREPARED.v3.json").open("x", encoding="utf-8") as output:
        json.dump(revised, output, indent=2)
    original = (ROOT / "verify_reference.py").read_text("utf-8")
    if original.count("PREPARED.v1.json") != 1 or original.count("REOPEN.v1.json") != 1:
        raise RuntimeError("Original verifier changed; inspect before adaptation")
    adapted = original.replace("PREPARED.v1.json", "PREPARED.v3.json").replace("REOPEN.v1.json", "REOPEN.v3.json")
    adapted = adapted.replace("assert d['unity_version']", "assert d['color_space']=='Gamma'\nassert d['unity_version']")
    adapted = adapted.replace("source_pins_unchanged=True,", "source_pins_unchanged=True,explicit_isolated_version_normalization_receipt=str(R/'VERSION-NORMALIZATION.v3.json'),original_verifier_sha256=sha(R/'verify_reference.py'),")
    verifier = ROOT / "verify_reference.v3.py"
    with verifier.open("x", encoding="utf-8") as output:
        output.write(adapted)
    with (ROOT / "reopen.v3.stdout.log").open("x", encoding="utf-8") as out, (ROOT / "reopen.v3.stderr.log").open("x", encoding="utf-8") as err:
        code = subprocess.run([sys.executable, str(verifier)], cwd=ROOT, stdout=out, stderr=err, check=False).returncode
    result = {"actual_independent_verifier_exit_code": code, "native_capture_rerun": False, "previous_refusal_preserved": True, "version_normalization_receipt": str(ROOT / "VERSION-NORMALIZATION.v3.json"), "verifier_sha256": sha(verifier), "full_import": False}
    if code == 0:
        report = read(ROOT / "REOPEN.v3.json")
        result.update(verified_textures=report["verified_textures"], verified_mip_levels=report["verified_mip_levels"], actual_bevy_comparison_qualified=False)
    with (ROOT / "REOPEN-RUN.v3.json").open("x", encoding="utf-8") as output:
        json.dump(result, output, indent=2)
    print(json.dumps(result))
    raise SystemExit(code)

if __name__ == "__main__":
    main()
