"""Preserve primary HDRP evidence explaining why direct debug inversion refused."""
from pathlib import Path
import datetime
import hashlib
import json

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'detail-debug-output-encoding-diagnosis-v1'
SDK=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime'

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

if __name__=='__main__':
    assert not ROOT.exists()
    material=SDK/'Debug/DebugDisplayMaterial.hlsl'
    pipeline=SDK/'RenderPipeline/HDRenderPipeline.Debug.cs'
    text=material.read_text('utf-8-sig')
    assert 'if (!needLinearToSRGB && _DebugAOVOutput == 0)' in text
    assert 'result = SRGBToLinear(max(0, result));' in text
    assert 'cb._DebugAOVOutput = aovOutput ? 1 : 0;' in pipeline.read_text('utf-8-sig')
    refused_path=W/'detail-default-native-rg-binding-v1/RESULT.v1.json'
    refused=json.loads(refused_path.read_text('utf-8-sig'))
    assert not refused['native_default_rg_binding_qualified']
    assert refused['error']=="AssertionError('Native response differs from source graph transfer')"
    ROOT.mkdir()
    for source,name in [(material,'DebugDisplayMaterial.hlsl'),(pipeline,'HDRenderPipeline.Debug.cs')]:
        (ROOT/name).write_bytes(source.read_bytes())
        assert sha(ROOT/name)==sha(source)
    result=dict(status='primary_hdrp_debug_srgb_conversion_identified',
        debug_surface_values_are_not_direct_linear_material_values=True,
        primary_condition='!needLinearToSRGB && _DebugAOVOutput == 0',
        primary_conversion='SRGBToLinear(max(0, result))',
        native_aov_path_skips_this_conversion=True,
        exact_intermediate_format_and_readback_encoding_qualified=False,
        native_default_rg_binding_qualified=False,original_refusal_preserved=True,
        next_action='Use the native HDRP AOV path or independently qualify its exact output conversion before deriving RG',
        input_pins={str(path):sha(path) for path in [material,pipeline,refused_path,Path(__file__).resolve()]},
        full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({key:value for key,value in result.items() if key!='input_pins'}))
