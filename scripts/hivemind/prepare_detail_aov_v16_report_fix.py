"""Retain v15 captures and cache the real pipeline identity before teardown."""
from pathlib import Path
import hashlib,json,subprocess
HERE=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
    prior=W/'detail-native-normal-loop-aov-v15';run=json.loads((prior/'RUN-RESULT.v1.json').read_bytes())
    assert run['unity_exit_code']==2 and run['private_input_configuration_restored'] and run['finished_utc']
    assert len(list((prior/'native-pixels').glob('*.rgba.f16')))==36
    failure=(prior/'NATIVE.v1.json.failure.txt').read_text('utf-8-sig');assert 'FinalizeReport' in failure and 'NullReferenceException' in failure
    old=HERE/'candidates/SourceDetailAovV15Probe.cs';new=HERE/'candidates/SourceDetailAovV16Probe.cs';assert not new.exists()
    text=old.read_text('utf-8').replace('V15','V16')
    needle='static string meshGuid,meshId,shaderId;';assert text.count(needle)==1;text=text.replace(needle,'static string meshGuid,meshId,shaderId,pipelineName;')
    needle='var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(request.prefab_path);';assert text.count(needle)==1
    text=text.replace(needle,'''if(!(RenderPipelineManager.currentPipeline is HDRenderPipeline))throw new Exception("Actual native HDRP identity absent");
   pipelineName=RenderPipelineManager.currentPipeline.GetType().FullName;
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(request.prefab_path);''')
    needle='pipeline=RenderPipelineManager.currentPipeline.GetType().FullName';assert text.count(needle)==1;text=text.replace(needle,'pipeline=pipelineName')
    new.write_text(text,'utf-8')
    queue=HERE/'queue_detail_native_aov_v16.py';assert not queue.exists()
    queue.write_text((HERE/'queue_detail_native_aov_v15.py').read_text('utf-8').replace('V15','V16').replace('v15','v16'),'utf-8')
    repair=json.loads((W/'detail-aov-private-input-compatibility-v1/REPAIR.v1.json').read_bytes());config=Path(repair['project_config'])
    ps='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
    assert subprocess.check_output([ps,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    assert sha(config)==repair['before_sha256'] and sha(repair['after'])==repair['after_sha256']
    config.write_bytes(Path(repair['after']).read_bytes());print(json.dumps(dict(new_helper=str(new),prior_images_preserved=36,private_config_restore_guard_retained=True)))
if __name__=='__main__':main()
