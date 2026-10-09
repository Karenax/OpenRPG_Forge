"""Serialize the repaired engine check behind existing Cargo jobs."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys
import time

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-ten-texture-engine-recovery-v1'


def save(data):
    data['updated_utc'] = datetime.now(timezone.utc).isoformat()
    path = ROOT / 'RUN-RESULT.v1.json'
    temp = path.with_suffix('.tmp')
    temp.write_text(json.dumps(data, indent=2), encoding='utf-8')
    temp.replace(path)


def main():
    if len(sys.argv) == 1:
        assert not ROOT.exists()
        ROOT.mkdir()
        fix = S.parents[1] / 'crates/forge_engine/src/source_unlit_forward.rs'
        save({'status': 'repaired_engine_check_queued_behind_existing_cargo', 'full_import': False,
              'complete_assets_delivered_increment': 0, 'constructor_fix': str(fix),
              'constructor_fix_sha256': hashlib.sha256(fix.read_bytes()).hexdigest(),
              'prior_failure': str(W / 'source-graph-ten-texture-engine-check-v1/RUN-RESULT.v1.json')})
        with (ROOT / 'controller.log').open('xb') as log:
            child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
        (ROOT / 'JOB.v1.json').write_text(json.dumps({'pid': child.pid}), encoding='utf-8')
        print(json.dumps({'pid': child.pid, 'root': str(ROOT)}))
        return
    state = json.loads((ROOT / 'RUN-RESULT.v1.json').read_text(encoding='utf-8'))
    try:
        while True:
            output = subprocess.check_output(['powershell', '-NoProfile', '-Command', '@(Get-Process cargo -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id) | ConvertTo-Json -Compress'], text=True).strip()
            pids = json.loads(output) if output else []
            if isinstance(pids, int):
                pids = [pids]
            if not pids:
                break
            if state.get('waiting_cargo_pids') != pids:
                state['waiting_cargo_pids'] = pids
                save(state)
            time.sleep(5)
        assert hashlib.sha256(Path(state['constructor_fix']).read_bytes()).hexdigest() == state['constructor_fix_sha256']
        subprocess.run([sys.executable, '-B', str(S / 'check_ten_texture_engine_v2.py')], check=True)
        engine = W / 'source-graph-ten-texture-engine-check-v2/RUN-RESULT.v1.json'
        for _ in range(40):
            if engine.exists():
                break
            time.sleep(0.5)
        assert engine.exists()
        subprocess.run([sys.executable, '-B', str(S / 'queue_native_capture_common_install_v2.py')], check=True)
        state.update(status='repaired_engine_check_and_guarded_install_dispatched', engine_receipt=str(engine), install_receipt=str(W / 'native-texture-capture-common-install-v2/RUN-RESULT.v1.json'))
    except Exception as error:
        state.update(status='recovery_dispatch_refusal_retained', error=repr(error))
    save(state)


if __name__ == '__main__':
    main()
