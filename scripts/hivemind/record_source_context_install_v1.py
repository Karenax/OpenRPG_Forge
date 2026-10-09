"""Retain actual context-module and SDK-definition pins; no GPU qualification."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'studio-source-context-install-v1'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    modules=[R/'apps/forge_studio/src/authoring_assets.rs',*[R/'apps/forge_studio/src/authoring_assets'/n for n in ['source_graph_host.rs','source_graph_context.rs']]]
    sdk=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache'
    definitions=[next(sdk.glob('com.unity.shadergraph@*/Editor/Data/Nodes/Input/Scene/ObjectNode.cs')),next(sdk.glob('com.unity.render-pipelines.high-definition@*/Runtime/RenderPipeline/Camera/HDCamera.cs'))]
    for p in modules+definitions:assert p.is_file(),p
    prior=json.loads((ROOT/'PREPARED.v1.json').read_text('utf-8-sig'))
    for original,digest in prior['source_pins'].items():
        backup=ROOT/(Path(original).name+'.preimage');assert sha(backup)==digest,backup
    result=dict(status='dynamic_context_adapter_installed_tests_pending',updated_utc=datetime.now(timezone.utc).isoformat(),installed_module_pins={str(p):sha(p) for p in modules},primary_sdk_definition_pins={str(p):sha(p) for p in definitions},definitions=dict(object_scale='lengths of UNITY_MATRIX_M columns',time='current and previous: seconds, sin(seconds), cos(seconds), zero'),native_test_receipt=str(W/'studio-source-context-native-v1/RUN-RESULT.v1.json'),web_receipt=str(W/'studio-current-source-wasm-check-v10/RUN-RESULT.v1.json'),viewport_mount_complete=False,camera_lod_qualified=False,gpu_rendered=False,editor_qualified=False,full_import=False)
    path=ROOT/'INSTALLED.v1.json';assert not path.exists(),'Preserve existing receipt'
    path.write_text(json.dumps(result,indent=2)+'\n','utf-8')
    print(json.dumps(dict(status=result['status'],module_count=len(modules),sdk_definition_count=len(definitions))))
if __name__=='__main__':main()
