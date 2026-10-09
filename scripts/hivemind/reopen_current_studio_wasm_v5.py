"""Independently reopen an exact frontend compile receipt, not UI/GPU qualification."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'studio-current-source-wasm-check-v5';OUT=W/'studio-current-wasm-independent-reopen-v5'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not OUT.exists()
    run_path=ROOT/'RUN-RESULT.v1.json';run_sha=sha(run_path);run=read(run_path);prepared=read(ROOT/'PREPARED.v1.json')
    assert run['status']=='actual_studio_wasm_frontend_compile_check_passed' and run['actual_exit_code']==0 and not run['changed_source_files']
    assert run['input_pins']==prepared['source_pins'] and len(run['input_pins'])==prepared['source_files']==1026
    for name,expected in run['input_pins'].items():assert sha(name)==expected, ('Current source changed since compile',name)
    assert sha(ROOT/'stdout.log')==run['stdout_sha256'] and sha(ROOT/'stderr.log')==run['stderr_sha256']
    command=read(ROOT/'COMMAND.v1.json');args=command['command']
    assert args[1:]==['check','--locked','--offline','-p','forge_studio','--target','wasm32-unknown-unknown','--no-default-features','--features','web']
    assert sha(run_path)==run_sha
    OUT.mkdir();result=dict(status='actual_studio_wasm_compile_receipt_and_all_current_source_pins_reopened',finished_utc=datetime.now(timezone.utc).isoformat(),source_files=1026,actual_exit_code=0,receipt_sha256=run_sha,actual_frontend_compile_qualified=True,browser_bundle_built=False,actual_editor_global_qualified=False,gpu_rendered=False,full_import=False,input_pins={str(p):sha(p) for p in [run_path,ROOT/'PREPARED.v1.json',ROOT/'COMMAND.v1.json',ROOT/'stdout.log',ROOT/'stderr.log',Path(__file__)]})
    (OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
