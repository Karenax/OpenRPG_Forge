"""Resume a confirmed stopped read-only audit using its pinned existing binary.

No rebuild, Vault write, old-state overwrite or unsupported completion claim.
Serialize bulk reads after the owned engine build to avoid competing disk load.
"""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys
import time

W=Path('F:/OpenRPG_ImportWork')
OLD=W/'asset-library-indexed-payload-integrity-v1'
ROOT=W/'asset-library-indexed-payload-integrity-resume-v2'
WAIT=W/'source-unlit-canonical-native-check-v1/RUN-RESULT.v1.json'
EXE=OLD/'payload-integrity.immutable.v1.exe'
PS=Path('C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe')


def sha(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def read(path):return json.loads(path.read_text('utf-8-sig'))
def save(name,data):
    p=ROOT/(name+'.tmp');p.write_text(json.dumps(data,indent=2),'utf-8');p.replace(ROOT/name)
def check(pins):
    for name,digest in pins.items():assert sha(Path(name))==digest,'Pinned auditor input changed: '+name
def old_handles():
    command="@(Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -eq 40724 -or $_.Name -like '*payload-integrity*' } | Select-Object ProcessId,Name,CreationDate) | ConvertTo-Json -Compress"
    text=subprocess.check_output([str(PS),'-NoProfile','-Command',command],text=True).strip()
    return json.loads(text) if text else []


def prepare():
    assert not ROOT.exists()
    observed=old_handles();assert not observed,'Preserve active auditor/controller handles'
    assert not (OLD/'PAYLOAD-RESULT.v1.json').exists(),'Retain and inspect existing final result first'
    old_state=read(OLD/'RUN-RESULT.v1.json')
    assert old_state['status']=='hashing_every_indexed_payload_and_referenced_archives'
    assert sha(EXE)==old_state['executable_sha256']=='d0f1fc1dcd48227fd5d10b5edb2d72d3ecd371dcf26e800d96ac2c6c3a441e2b'
    pins={row['path']:row['sha256'] for row in read(OLD/'HELPER-PINS.v1.json')}
    pins.update({str(p):sha(p) for p in [EXE,OLD/'RUN-RESULT.v1.json',OLD/'HELPER-PINS.v1.json',Path(__file__).resolve()]})
    check(pins);ROOT.mkdir()
    save('STOPPED-PREDECESSOR.v1.json',dict(observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        recorded_controller_pid=40724,observed_matching_handles=observed,final_result_missing=True,
        old_state_preserved=True,actual_previous_exit_code_unknown=True))
    save('PREPARED.v1.json',dict(input_pins=pins,wait_for_owned_engine_result=str(WAIT),
        scope='indexed_payloads_and_referenced_archives',current_whole_vault_readback_qualified=False,
        only_readonly_vault_operations=True,full_import=False))
    with (ROOT/'controller.log').open('xb') as out:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdin=subprocess.DEVNULL,stdout=out,stderr=out,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,binary_rebuilt=False)))


def run():
    state=dict(status='waiting_for_owned_engine_before_bulk_integrity_reads',full_import=False,
        indexed_payload_integrity_qualified=False,whole_current_vault_qualified=False)
    save('RUN-RESULT.v1.json',state)
    try:
        while not WAIT.is_file() or not read(WAIT).get('finished_utc'):time.sleep(15)
        prepared=read(ROOT/'PREPARED.v1.json');check(prepared['input_pins']);assert not old_handles()
        output=ROOT/'PAYLOAD-RESULT.v1.json';assert not output.exists()
        with (ROOT/'execution.stdout.log').open('xb') as out,(ROOT/'execution.stderr.log').open('xb') as err:
            child=subprocess.Popen([str(EXE),str(output)],cwd=ROOT,
                stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='hashing_indexed_payloads_and_referenced_archives',audit_pid=child.pid)
            save('RUN-RESULT.v1.json',state);code=child.wait()
        state['actual_exit_code']=code;assert code==0,'Actual payload audit refused; retain per-entry result'
        report=read(output);assert report['refused_count']==0 and report['indexed_count']==report['passed_count']
        assert not report['full_import'];check(prepared['input_pins'])
        state.update(status='indexed_payloads_and_referenced_archive_sha_verified',indexed_payload_integrity_qualified=True,
            entries_verified=report['passed_count'],archives_hashed=report['unique_archive_payloads_hashed'],
            registry_revision=report['revision'],payload_receipt_sha256=sha(output))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified',error=repr(error),diagnostic_traceback=traceback.format_exc())
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)


if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
