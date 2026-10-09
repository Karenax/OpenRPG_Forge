"""Persist every 48-card audit batch, using read-lock-only JSON byte reuse.

Old interrupted audits and their immutable executables remain untouched. This
audit checks library records and images, never Unity appearance or full payloads.
"""
import os
from pathlib import Path
import subprocess
import sys

import queue_hdrp_snapshot_v4 as common

ROOT = common.W / 'library-checkpointed-native-audit-v2'
OLD = common.W / 'library-locked-integrity-reader-integration-v1'
common.ROOT = ROOT

BYTES_HELPER = r'''
struct CheckedBytes { _read_lock: File, bytes: Vec<u8> }

pub fn scoped_bytes(path: &Path, limit: u64) -> Option<Result<Vec<u8>, String>> {
    ACTIVE.with(|active| {
        let mut active = active.borrow_mut();
        let batch = active.as_mut()?;
        Some((|| {
            if let Some(held) = batch.bytes.get(path) {
                if held.bytes.len() as u64 > limit { return Err("Scoped bytes exceed request bound".into()); }
                batch.byte_hits += 1;
                return Ok(held.bytes.clone());
            }
            let mut options = OpenOptions::new(); options.read(true);
            #[cfg(windows)] { use std::os::windows::fs::OpenOptionsExt; options.share_mode(1); }
            let mut file = options.open(path).map_err(|e| format!("Byte read lock refused: {e}"))?;
            let metadata = file.metadata().map_err(|e| e.to_string())?;
            if !metadata.is_file() || metadata.len() > limit { return Err("Scoped bytes absent or too large".into()); }
            let mut bytes = Vec::new();
            (&mut file).take(limit.saturating_add(1)).read_to_end(&mut bytes).map_err(|e| e.to_string())?;
            if bytes.len() as u64 != metadata.len() || bytes.len() as u64 > limit { return Err("Scoped byte size changed".into()); }
            let answer = bytes.clone();
            batch.bytes.insert(path.to_path_buf(), CheckedBytes { _read_lock: file, bytes });
            Ok(answer)
        })())
    })
}

#[cfg(all(test, windows))]
mod byte_tests {
    use super::*;
    #[test] fn locked_bytes_deny_mutation_and_respect_stricter_bounds() {
        let root = std::env::temp_dir().join(format!("byte-audit-{}", std::process::id()));
        std::fs::create_dir_all(&root).unwrap(); let path = root.join("owned-read-control.json");
        std::fs::write(&path, b"original-json").unwrap();
        with_locked_integrity_batch(|| {
            assert_eq!(scoped_bytes(&path,64).unwrap()?, b"original-json");
            assert_eq!(scoped_bytes(&path,64).unwrap()?, b"original-json");
            assert!(scoped_bytes(&path,2).unwrap().is_err());
            assert!(OpenOptions::new().write(true).open(&path).is_err());
            assert!(std::fs::remove_file(&path).is_err()); Ok(())
        }).unwrap();
        std::fs::write(&path,b"changed-json").unwrap();
        assert!(scoped_bytes(&path,64).is_none());
        with_locked_integrity_batch(|| {assert_eq!(scoped_bytes(&path,64).unwrap()?,b"changed-json");Ok(())}).unwrap();
    }
    #[test] fn failed_read_scope_releases_lock_and_returns_no_persistent_bytes() {
        let path = std::env::temp_dir().join(format!("byte-failed-audit-{}.json",std::process::id()));
        std::fs::write(&path,b"owned").unwrap();
        let result: Result<((),serde_json::Value),String> = with_locked_integrity_batch(|| {
            scoped_bytes(&path,64).unwrap()?; Err("controlled failure".into())
        });
        assert!(result.is_err()); assert!(scoped_bytes(&path,64).is_none());
        assert!(OpenOptions::new().write(true).open(&path).is_ok());
    }
}
'''


def prepare():
    assert not ROOT.exists(), 'Existing checkpointed audit preserved'
    old = common.read(OLD / 'PREPARED.v1.json')
    common.check(old['source_pins'])
    native = (OLD / 'src/asset_library_native.rs').read_text('utf-8-sig')
    locked = (OLD / 'src/locked_integrity_batch.rs').read_text('utf-8-sig')
    main = (OLD / 'src/main.rs').read_text('utf-8-sig')
    marker = 'fn read_limited(path: &Path, limit: u64) -> Result<Vec<u8>, String> {'
    assert native.count(marker) == 1
    native = native.replace(marker, marker + '\n    if path.extension().is_some_and(|ext| ext == "json") && limit <= 64 * 1024 * 1024 { if let Some(result) = crate::locked_integrity_batch::scoped_bytes(path, limit) { return result; } }', 1)
    assert locked.count('    bytes_hashed: u64,') == 1
    locked = locked.replace('    bytes_hashed: u64,', '    bytes_hashed: u64,\n    bytes: HashMap<PathBuf, CheckedBytes>,\n    byte_hits: u64,', 1)
    locked = locked.replace('"persistent_cache":false,"full_import":false',
        '"unique_json_byte_reads":batch.bytes.len(),"repeated_json_reads_avoided":batch.byte_hits,"persistent_cache":false,"full_import":false', 1)
    locked += BYTES_HELPER
    marker = ' loop{\n'
    assert main.count(marker) == 1
    main = main.replace(marker, ' let checkpoint_dir=output.with_file_name("batches");std::fs::create_dir(&checkpoint_dir).unwrap();\n loop{\n  let row_start=rows.len();let images_start=images;\n', 1)
    marker = '  offset+=page.assets.len();'
    assert main.count(marker) == 1
    replacement = '''  let checkpoint=checkpoint_dir.join(format!("{:06}-{:06}.json",offset,offset+page.assets.len()-1));
  save(&checkpoint,&json!({"schema":"openrpg.native-library-audit-batch.v2","revision":page.revision,"expected_total":total,"offset":offset,"details_checked":page.assets.len(),"images_read":images-images_start,"rows":&rows[row_start..],"only_library_records_and_image_bytes":true,"gpu_qualified":false,"full_import":false}));
  let checkpoint_sha=format!("{:x}",Sha256::digest(std::fs::read(&checkpoint).unwrap()));
  eprintln!("{}",json!({"checkpoint":checkpoint,"sha256":checkpoint_sha,"details_checked":offset+page.assets.len(),"total":total,"images_read":images}));
  offset+=page.assets.len();'''
    main = main.replace(marker, replacement, 1)
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    for name, contents in [('asset_library_native.rs', native), ('locked_integrity_batch.rs', locked), ('main.rs', main)]:
        (ROOT / 'src' / name).write_text(contents, encoding='utf-8')
    cargo = (OLD / 'Cargo.toml').read_text('utf-8-sig').replace('openrpg-locked-library-reader-integration-v1','openrpg-checkpointed-library-audit-v2')
    (ROOT / 'Cargo.toml').write_text(cargo, encoding='utf-8')
    (ROOT / 'Cargo.lock').write_bytes((OLD / 'Cargo.lock').read_bytes())
    pins = {**old['source_pins'], str(Path(__file__).resolve()): common.sha(__file__)}
    for path in [*sorted((ROOT / 'src').glob('*.rs')), ROOT / 'Cargo.toml']:
        pins[str(path)] = common.sha(path)
    common.check(pins)
    common.save('PREPARED.v2.json', dict(input_pins=pins, batch_size=48,
        prior_interrupted_audit=str(OLD), prior_logged_reads_not_promoted=10320,
        persistent_integrity_cache=False, byte_reuse_requires_os_write_delete_denial=True,
        canonical_product_modified=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    common.save('JOB.v2.json', dict(controller_pid=child.pid, created_utc=common.now()))
    print(dict(controller_pid=child.pid, batch_size=48, expected_cards=26735))


def run():
    state = dict(status='prechecking_checkpointed_audit', full_import=False, gpu_render_parity_qualified=False)
    common.save('RUN-RESULT.v2.json', state)
    try:
        prep = common.read(ROOT / 'PREPARED.v2.json')
        common.check(prep['input_pins'])
        env = os.environ.copy()
        env.update(CARGO_TARGET_DIR=str(common.W / 'asset-library-all-entries-native-audit-v1/target'),
            CARGO_INCREMENTAL='0', TEMP=str(ROOT), TMP=str(ROOT))
        cargo = Path('C:/Users/karen/.cargo/bin/cargo.exe')
        for stage, args in [('testing', ['test','--offline','-j1','--','--test-threads=1']),
                ('building', ['build','--offline','-j1'])]:
            state['status'] = stage + '_checkpointed_native_reader'
            common.save('RUN-RESULT.v2.json', state)
            with (ROOT / (stage + '.log')).open('xb') as log:
                code = subprocess.run([str(cargo), '+1.98.0', *args], cwd=ROOT,
                    env=env, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW).returncode
            state[stage + '_exit_code'] = code
            assert code == 0, stage + ' refused; inspect retained log'
        common.check(prep['input_pins'])
        built = Path(env['CARGO_TARGET_DIR']) / 'debug/openrpg-checkpointed-library-audit-v2.exe'
        executable = ROOT / 'library-checkpointed.immutable.v2.exe'
        with executable.open('xb') as stream:
            stream.write(built.read_bytes())
        state.update(executable_sha256=common.sha(executable), status='auditing_all_cards_with_48_card_checkpoints')
        common.save('RUN-RESULT.v2.json', state)
        with (ROOT / 'audit.stdout.log').open('xb') as out, (ROOT / 'audit.stderr.log').open('xb') as err:
            child = subprocess.Popen([str(executable), str(ROOT / 'RESULT.v2.json')], cwd=ROOT,
                env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state['audit_pid'] = child.pid
            common.save('RUN-RESULT.v2.json', state)
            state['audit_exit_code'] = child.wait()
        common.check(prep['input_pins'])
        assert state['audit_exit_code'] == 0, 'Native reader audit refused'
        report = common.read(ROOT / 'RESULT.v2.json')
        assert report['entries_checked'] == len(report['rows']) == 26735
        state.update(status='all_current_library_records_and_declared_images_audited',
            entries_checked=report['entries_checked'], result_sha256=common.sha(ROOT / 'RESULT.v2.json'))
    except Exception as error:
        state.update(status='refused_or_interrupted', error=f'{type(error).__name__}: {error}')
    state['finished_utc'] = common.now()
    common.save('RUN-RESULT.v2.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
