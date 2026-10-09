"""Test opt-in locked hashing in an isolated copy of the full native reader."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "library-locked-integrity-reader-integration-v1"
OLD = W / "asset-library-all-entries-native-audit-v2"
CACHE = W / "library-locked-integrity-batch-candidate-v1"
TOOLCHAIN = Path(r"C:\Users\karen\.rustup\toolchains\1.98.0-x86_64-pc-windows-msvc\bin")
REPO = Path(r"E:\OpenRPG_Forge\OpenRPG_Forge-Rust")
SCOPE = Path(__file__).parent / "candidates/locked_integrity_batch.rs"

EXTRA_TESTS = r'''
    #[test]
    fn locked_batch_real_source_details_share_hash_and_protect_source() {
        let (root, _) = fixture(); let id = add_source_support(&root);
        let mut library = Library::new(&root).unwrap();
        let (_, stats) = crate::locked_integrity_batch::with_locked_integrity_batch(|| {
            let LibraryResponse::Page{page} = library.execute(LibraryRequest::Search{query:LibraryQuery{
                availability:"source_support".into(),..Default::default()}})? else {panic!()};
            let request = || LibraryRequest::Details{id:id.clone(),revision:page.revision.clone()};
            let LibraryResponse::Details{detail} = library.execute(request())? else {panic!()};
            let path = PathBuf::from(detail.source_path.unwrap());
            assert!(fs::OpenOptions::new().write(true).open(&path).is_err());
            assert!(matches!(library.execute(request())?,LibraryResponse::Details{..}));
            Ok(())
        }).unwrap();
        assert_eq!(stats["unique_payloads_hashed"],2);
        assert!(stats["repeated_full_hash_reads_avoided"].as_u64().unwrap()>=2);
    }
    #[test]
    fn locked_batch_end_restores_fresh_interactive_source_refusal() {
        let (root, _) = fixture(); let id = add_source_support(&root);
        let mut library = Library::new(&root).unwrap();
        let LibraryResponse::Page{page} = library.execute(LibraryRequest::Search{query:LibraryQuery{
            availability:"source_support".into(),..Default::default()}}).unwrap() else {panic!()};
        let request = || LibraryRequest::Details{id:id.clone(),revision:page.revision.clone()};
        let (path, _) = crate::locked_integrity_batch::with_locked_integrity_batch(|| {
            let LibraryResponse::Details{detail} = library.execute(request())? else {panic!()};
            Ok(PathBuf::from(detail.source_path.unwrap()))
        }).unwrap();
        fs::write(&path,b"changed after locked session").unwrap();
        assert!(library.execute(request()).unwrap_err().contains("empreinte"));
    }
'''

def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def save(name, data):
    p = ROOT / name
    temp = p.with_name(p.name + f".{os.getpid()}.tmp")
    temp.write_text(json.dumps(data, indent=2), "utf-8")
    os.replace(temp, p)

def prepare():
    if ROOT.exists():
        raise RuntimeError("Existing integration candidate preserved")
    proof = json.loads((CACHE / "RUN-RESULT.v1.json").read_text("utf-8-sig"))
    if proof["status"] != "five_windows_locked_hash_batch_controls_verified" or proof["controls_exit_code"] != 0 or not proof["source_pins_unchanged"]:
        raise RuntimeError("Five actual Windows lock controls required")
    prior = json.loads((CACHE / "PREPARED.v1.json").read_text("utf-8-sig"))
    if sha(SCOPE) != prior["source_pins"][str(SCOPE)]:
        raise RuntimeError("Verified scope candidate changed")
    (ROOT / "src").mkdir(parents=True)
    (ROOT / "temp").mkdir()
    native_source = OLD / "src/asset_library_native.rs"
    native = native_source.read_text("utf-8-sig")
    definition = "fn same_file_hash(path: &Path, expected: &str, limit: u64) -> Result<bool, String> {"
    if native.count(definition) != 1 or native.count("mod tests {") != 1:
        raise RuntimeError("Native reader shape changed")
    native = native.replace(definition, '''fn same_file_hash(path: &Path, expected: &str, limit: u64) -> Result<bool, String> {
    if let Some(result) = crate::locked_integrity_batch::scoped_file_hash(path, expected, limit) { return result; }
    ordinary_same_file_hash(path, expected, limit)
}
fn ordinary_same_file_hash(path: &Path, expected: &str, limit: u64) -> Result<bool, String> {''')
    native = native.replace("mod tests {", "mod tests {\n" + EXTRA_TESTS, 1)
    (ROOT / "src/asset_library_native.rs").write_text(native, "utf-8")
    (ROOT / "src/locked_integrity_batch.rs").write_bytes(SCOPE.read_bytes())
    main = (OLD / "src/main.rs").read_text("utf-8-sig")
    if main.count("fn main(){") != 1:
        raise RuntimeError("Full audit wrapper changed")
    main = "mod locked_integrity_batch;\n" + main.replace("fn main(){", '''fn main(){
 let (_,stats)=locked_integrity_batch::with_locked_integrity_batch(||{audit_main();Ok(())}).unwrap();
 let output=std::path::PathBuf::from(std::env::args().nth(1).unwrap());
 save(&output.with_file_name("LOCKED-SESSION.v1.json"),&stats);
}
fn audit_main(){''', 1)
    (ROOT / "src/main.rs").write_text(main, "utf-8")
    cargo = (OLD / "Cargo.toml").read_text("utf-8-sig").replace("openrpg-all-library-entries-audit-v2", "openrpg-locked-library-reader-integration-v1")
    (ROOT / "Cargo.toml").write_text(cargo, "utf-8")
    source_paths = [SCOPE, native_source, OLD / "src/main.rs", OLD / "Cargo.toml", CACHE / "RUN-RESULT.v1.json", Path(__file__).resolve()]
    app = REPO / "apps/forge_studio/src"
    source_paths += [app / "asset_library_protocol.rs", app / "asset_library_model_wire.rs"]
    source_paths += list((app / "asset_library_native").rglob("*.rs"))
    source_paths += list((app / "asset_library_model_wire").rglob("*.rs"))
    for crate in ["forge_core"]:
        base = REPO / "crates" / crate
        source_paths += [base / "Cargo.toml", *list((base / "src").rglob("*.rs"))]
    source_paths += [*list((ROOT / "src").glob("*.rs")), ROOT / "Cargo.toml"]
    pins = {str(p): sha(p) for p in source_paths}
    save("PREPARED.v1.json", {"source_pins": pins, "canonical_installed": False,
         "full_audit_wrapper_scope_preserved": True, "existing_full_audit_stopped": False, "full_import": False})
    save("RUN-RESULT.v1.json", {"status": "integration_tests_queued", "canonical_installed": False, "full_import": False})
    with (ROOT / "controller.log").open("xb") as log:
        child = subprocess.Popen([sys.executable, str(Path(__file__).resolve()), "run"], cwd=ROOT,
            stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    save("JOB.v1.json", {"pid": child.pid, "created_utc": now()})
    print(json.dumps({"integration_controller_pid": child.pid, "full_audit_not_started": True}))

def run():
    pins = json.loads((ROOT / "PREPARED.v1.json").read_text("utf-8"))["source_pins"]
    result = {"status": "testing_native_reader_locked_hash_integration", "started_utc": now(),
              "canonical_installed": False, "full_library_audit_executed": False, "full_import": False}
    save("RUN-RESULT.v1.json", result)
    env = os.environ.copy()
    env.update(RUSTC=str(TOOLCHAIN / "rustc.exe"), RUSTDOC=str(TOOLCHAIN / "rustdoc.exe"),
               RUSTUP_TOOLCHAIN="1.98.0-x86_64-pc-windows-msvc", CARGO_INCREMENTAL="0",
               CARGO_TARGET_DIR=str(OLD / "target"), TEMP=str(ROOT / "temp"), TMP=str(ROOT / "temp"))
    env["PATH"] = str(TOOLCHAIN) + os.pathsep + env.get("PATH", "")
    try:
        if any(sha(Path(p)) != expected for p, expected in pins.items()):
            raise RuntimeError("Prepared integration inputs changed")
        for stage, args in [("lock", ["generate-lockfile", "--offline"]),
                            ("native-tests", ["test", "--locked", "--offline", "-j1", "--", "--nocapture"])]:
            with (ROOT / f"{stage}.stdout.log").open("wb") as out, (ROOT / f"{stage}.stderr.log").open("wb") as err:
                process = subprocess.Popen([str(TOOLCHAIN / "cargo.exe"), *args], cwd=ROOT, env=env,
                    stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
                save("NATIVE-JOB.v1.json", {"pid": process.pid, "stage": stage, "created_utc": now()})
                code = process.wait()
            result[f"{stage}_exit_code"] = code
            if code:
                raise RuntimeError(f"{stage} failed; logs retained")
        output = (ROOT / "native-tests.stdout.log").read_text("utf-8-sig")
        counts = [int(m) for m in re.findall(r"test result: ok\. (\d+) passed; 0 failed", output)]
        if not counts or max(counts) < 7:
            raise RuntimeError("Actual reader and lock tests not proven")
        for label in ["locked_batch_real_source_details_share_hash_and_protect_source", "locked_batch_end_restores_fresh_interactive_source_refusal"]:
            if not re.search(r"test .*" + label + r" \.\.\. ok", output):
                raise RuntimeError("Real reader integration control missing")
        if any(sha(Path(p)) != expected for p, expected in pins.items()):
            raise RuntimeError("Inputs changed during native controls")
        result.update(status="native_reader_locked_hash_integration_verified", actual_tests_passed=max(counts),
                      prepared_source_pins_unchanged=True,
                      stdout_sha256=sha(ROOT / "native-tests.stdout.log"), stderr_sha256=sha(ROOT / "native-tests.stderr.log"))
    except Exception as error:
        result.update(status="refused_or_unverified", error=str(error))
    result["finished_utc"] = now()
    save("RUN-RESULT.v1.json", result)
    print(json.dumps(result))
    if result["status"] == "refused_or_unverified":
        raise SystemExit(1)

if __name__ == "__main__":
    {"prepare": prepare, "run": run}[sys.argv[1]]()
