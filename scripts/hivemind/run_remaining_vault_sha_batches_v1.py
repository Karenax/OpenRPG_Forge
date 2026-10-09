"""Run remaining physical SHA batches sequentially, resume only from checked receipts."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os
import subprocess
import sys

OUT = Path('F:/OpenRPG_ImportWork/vault-indexed-coverage-plan-v1')


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    plan_path = OUT / 'RESULT.v1.json'
    plan = json.loads(plan_path.read_text('utf-8-sig'))
    helper = Path(__file__).with_name('check_physical_vault_batch_v1.py')
    helper_pin = sha(helper)
    run_path = OUT / 'RUN-RESULT.v1.json'
    assert not run_path.exists(), 'Inspect terminal/live owner before resuming an existing controller'
    state = dict(status='running', controller_pid=os.getpid(), started_utc=datetime.now(timezone.utc).isoformat(),
                 completed_batches=0, checked_files=0, checked_bytes=0, batch_count=len(plan['remaining_batches']),
                 plan_sha256=sha(plan_path), helper_sha256=helper_pin, whole_current_vault_qualified=False, full_import=False)
    def publish():
        temp = run_path.with_suffix('.tmp')
        temp.write_text(json.dumps(state, ensure_ascii=False, indent=2), 'utf-8')
        temp.replace(run_path)
    publish()
    for number, batch in enumerate(plan['remaining_batches']):
        path = Path(batch['path'])
        assert sha(path) == batch['sha256'] and sha(helper) == helper_pin
        receipt_path = OUT / 'checks' / f'{number:05d}.RESULT.v1.json'
        if not receipt_path.exists():
            log = OUT / 'checks' / f'{number:05d}.log'
            log.parent.mkdir(exist_ok=True)
            with log.open('x', encoding='utf-8') as stream:
                child = subprocess.Popen([sys.executable, str(helper), str(number), 'remaining'], stdout=stream, stderr=subprocess.STDOUT)
                state.update(active_batch=number, child_pid=child.pid)
                publish()
                exit_code = child.wait()
            if exit_code:
                state.update(status='batch_execution_failed', actual_child_exit_code=exit_code, log=str(log))
                publish()
                raise SystemExit(exit_code)
        receipt = json.loads(receipt_path.read_text('utf-8-sig'))
        ledger = receipt_path.with_suffix('.jsonl')
        if receipt['errors'] or receipt['checked_files'] != batch['files'] or receipt['plan_sha256'] != batch['sha256'] or sha(ledger) != receipt['ledger_sha256']:
            state.update(status='batch_receipt_refused', failed_receipt=str(receipt_path))
            publish()
            raise SystemExit(1)
        state['completed_batches'] += 1
        state['checked_files'] += receipt['checked_files']
        state['checked_bytes'] += receipt['checked_bytes']
        state['last_completed_batch'] = number
        state['last_checkpoint_utc'] = datetime.now(timezone.utc).isoformat()
        state.pop('child_pid', None)
        publish()
        print(f"SHA batch {number + 1}/{len(plan['remaining_batches'])}: {receipt['checked_files']} files verified", flush=True)
    state.update(status='all_uncovered_physical_sha_batches_completed', finished_utc=datetime.now(timezone.utc).isoformat())
    state.pop('active_batch', None)
    publish()
    print('Remaining actual SHA reads complete; final inventory stability and historical reconciliation still required.', flush=True)


if __name__ == '__main__':
    main()
