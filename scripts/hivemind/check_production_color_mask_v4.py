"""Resolve a separate offline lock, then run locked actual renderer unit tests.

Keep every v3 source byte and host assertion; preserve old lock and refusal.
"""
from pathlib import Path
import datetime
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import tomllib

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'source-graph-production-color-mask-check-v4'
OLD = W / 'source-graph-production-color-mask-snapshot-v3'
SNAPSHOT = W / 'source-graph-production-color-mask-snapshot-v4'
PRIOR = W / 'source-graph-production-color-mask-check-v3'
WAIT = W / 'delta-firepit-ra-shader-gpu-v2/RUN-RESULT.v1.json'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TARGET = Path('F:/OpenRPG_Forge/relocated-work/Rust-target')

def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def check(pins):
    for p, digest in pins.items(): assert sha(Path(p)) == digest, 'Changed source: ' + p
def save(name, data):
    p = ROOT / (name + '.tmp'); p.write_text(json.dumps(data, indent=2), 'utf-8'); p.replace(ROOT / name)

def run():
    state = dict(status='waiting_for_owned_gpu_job', host_tests_passed=17,
        actual_renderer_tests_passed=0, canonical_installed=False, full_import=False)
    save('RUN-RESULT.v1.json', state)
    try:
        while not WAIT.is_file() or not read(WAIT).get('finished_utc'): time.sleep(15)
        old_result = read(PRIOR / 'RUN-RESULT.v1.json')
        assert old_result['host_tests_passed'] == 17 and old_result['actual_renderer_tests_passed'] == 0
        prepared = read(PRIOR / 'PREPARED.v1.json')
        pins = dict(prepared['input_pins']); pins.update(prepared['frozen_pins'])
        for p in [PRIOR/'PREPARED.v1.json', PRIOR/'RUN-RESULT.v1.json', WAIT, Path(__file__).resolve()]: pins[str(p)] = sha(p)
        check(pins)
        assert not SNAPSHOT.exists(); SNAPSHOT.mkdir()
        frozen = {}
        for name, digest in prepared['frozen_pins'].items():
            source = Path(name)
            if not source.is_relative_to(OLD): continue
            relative = source.relative_to(OLD); target = SNAPSHOT / relative
            target.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(source, target)
            assert sha(target) == digest
            if relative.as_posix() != 'Cargo.lock': frozen[str(target)] = digest
        shutil.copyfile(OLD/'Cargo.lock', ROOT/'Cargo.lock.before')
        save('SOURCE-SNAPSHOT.v1.json', dict(input_pins=pins, frozen_source_pins=frozen,
            host_result_reference=str(PRIOR/'RUN-RESULT.v1.json'), host_tests_reused=17))
        env = os.environ.copy(); env.update(RUSTC=str(BIN/'rustc.exe'), TEMP=str(ROOT/'temp'), TMP=str(ROOT/'temp'),
            CARGO_TARGET_DIR=str(TARGET), CARGO_BUILD_JOBS='1', PATH=str(BIN)+os.pathsep+env.get('PATH',''))
        def command(label, args):
            state['status'] = label; save('RUN-RESULT.v1.json', state)
            with (ROOT/(label+'.stdout.log')).open('xb') as out, (ROOT/(label+'.stderr.log')).open('xb') as err:
                process = subprocess.Popen([str(BIN/'cargo.exe')]+args, cwd=ROOT, env=env,
                    stdin=subprocess.DEVNULL, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
                state['cargo_pid']=process.pid; save('RUN-RESULT.v1.json', state); code=process.wait()
            state[label+'_exit_code']=code; assert code==0, 'Actual Cargo command refused: '+label
            return (ROOT/(label+'.stdout.log')).read_text('utf-8-sig')
        command('resolve_separate_lock_offline', ['generate-lockfile','--offline','--manifest-path',str(SNAPSHOT/'Cargo.toml')])
        def packages(p):
            return {json.dumps(row,sort_keys=True) for row in tomllib.loads(p.read_text('utf-8'))['package']}
        before=packages(ROOT/'Cargo.lock.before'); after=packages(SNAPSHOT/'Cargo.lock')
        save('LOCK-RESOLUTION.v1.json', dict(old_lock_sha256=sha(ROOT/'Cargo.lock.before'),
            resolved_lock_sha256=sha(SNAPSHOT/'Cargo.lock'),
            removed_or_changed_packages=[json.loads(x) for x in sorted(before-after)],
            added_or_changed_packages=[json.loads(x) for x in sorted(after-before)],
            offline=True, verification_locked=True, source_changes=False))
        frozen[str(SNAPSHOT/'Cargo.lock')] = sha(SNAPSHOT/'Cargo.lock'); check(pins); check(frozen)
        save('PREPARED.v1.json',dict(input_pins=pins,frozen_pins=frozen,host_tests_passed=17,
            prior_refusal_preserved=True,canonical_installed=False,full_import=False))
        output=command('checking_actual_renderer_source_graph_tests', ['test','--manifest-path',str(SNAPSHOT/'Cargo.toml'),
            '-p','forge_engine','--lib','--locked','--offline','-j','1','source_graph','--','--test-threads=1'])
        match=re.search(r'test result: ok\. (\d+) passed; 0 failed;',output); assert match
        for name in ['schema_four_rgb_reaches_actual_material_program_and_legacy_keeps_all',
            'unknown_mask_missing_mask_or_mask_in_legacy_contract_refuse',
            'original_rgb_mask_preserves_secondary_attachment_and_changes_specialization_key',
            'invalid_channels_or_attachments_refuse_without_partial_state']:
            assert re.search(r'::'+name+r' \.\.\. ok',output), 'Missing actual renderer test: '+name
        check(pins); check(frozen)
        state.update(status='actual_host_and_renderer_mask_integration_tests_passed',actual_renderer_tests_passed=int(match.group(1)),
            source_pins_unchanged=True,frozen_pins_unchanged=True,lock_resolution_receipt_sha256=sha(ROOT/'LOCK-RESOLUTION.v1.json'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error),diagnostic_traceback=traceback.format_exc())
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat(); save('RUN-RESULT.v1.json',state)

if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='run': run()
    else:
        assert not ROOT.exists() and not SNAPSHOT.exists(); ROOT.mkdir(); (ROOT/'temp').mkdir()
        with (ROOT/'controller.log').open('xb') as out:
            child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
                stdin=subprocess.DEVNULL,stdout=out,stderr=out,creationflags=subprocess.CREATE_NO_WINDOW)
        save('JOB.v1.json',dict(controller_pid=child.pid)); print(json.dumps(dict(controller_pid=child.pid)))
