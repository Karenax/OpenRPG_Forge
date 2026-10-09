"""Enable both input APIs only in the private diagnostic project, with rollback."""
from pathlib import Path
import hashlib
import json
import subprocess
HERE=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork')
P=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
REPAIR=W/'detail-aov-private-input-compatibility-v1'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
    assert not REPAIR.exists()
    ps='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
    assert subprocess.check_output([ps,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    run=json.loads((W/'detail-native-normal-loop-aov-v14/RUN-RESULT.v1.json').read_bytes());assert run['finished_utc'] and run['unity_exit_code']!=0
    assert json.loads((W/'detail-native-texture-storage-v1/RUN-RESULT.v1.json').read_bytes())['textures_verified']==2
    config=P/'ProjectSettings/ProjectSettings.asset';raw=config.read_bytes();needle=b'  activeInputHandler: -1'
    assert raw.count(needle)==1
    expected=json.loads((W/'detail-default-native-gain-v11/PREPARED.v1.json').read_bytes())['input_pins'][str(config)]
    assert sha(config)==expected
    REPAIR.mkdir();before=REPAIR/'ProjectSettings.before.asset';after=REPAIR/'ProjectSettings.compatibility.asset'
    before.write_bytes(raw);compat=raw.replace(needle,b'  activeInputHandler: 2');after.write_bytes(compat)
    receipt=dict(project_config=str(config),before=str(before),before_sha256=sha(before),after=str(after),after_sha256=sha(after),
                 changed_field='activeInputHandler',old_value=-1,new_value=2,private_context_only=True,full_import=False)
    (REPAIR/'REPAIR.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
    old=HERE/'candidates/SourceDetailAovV14Probe.cs';helper=HERE/'candidates/SourceDetailAovV15Probe.cs';assert not helper.exists()
    helper.write_text(old.read_text('utf-8').replace('V14','V15'),'utf-8')
    queue=HERE/'queue_detail_native_aov_v15.py';assert not queue.exists()
    text=(HERE/'queue_detail_native_aov_v14.py').read_text('utf-8').replace('V14','V15').replace('v14','v15')
    oldline="pins=read(prior)['input_pins'];check(pins)"
    assert text.count(oldline)==1
    text=text.replace(oldline,"""pins=read(prior)['input_pins']
    repair_path=W/'detail-aov-private-input-compatibility-v1/REPAIR.v1.json';repair=read(repair_path)
    assert pins[repair['project_config']]==repair['before_sha256'] and sha(repair['before'])==repair['before_sha256']
    assert sha(repair['project_config'])==repair['after_sha256']
    pins[repair['project_config']]=repair['after_sha256']
    pins[str(repair_path)]=sha(repair_path);pins[repair['before']]=repair['before_sha256'];pins[repair['after']]=repair['after_sha256']
    check(pins)""")
    needle="if broker is not None:ns['stop_broker'](broker,state)"
    assert text.count(needle)==1
    text=text.replace(needle,"""if broker is not None:ns['stop_broker'](broker,state)
        repair=read(W/'detail-aov-private-input-compatibility-v1/REPAIR.v1.json')
        config=Path(repair['project_config'])
        if sha(config)==repair['after_sha256'] and sha(repair['before'])==repair['before_sha256']:
            config.write_bytes(Path(repair['before']).read_bytes())
            state['private_input_configuration_restored']=sha(config)==repair['before_sha256']
        else:
            state.update(private_input_configuration_restored=False,configuration_restore_refused='Unexpected private configuration bytes; preserve them')""")
    queue.write_text(text,'utf-8')
    config.write_bytes(compat);assert sha(config)==receipt['after_sha256']
    print(json.dumps(receipt))
if __name__=='__main__':main()
