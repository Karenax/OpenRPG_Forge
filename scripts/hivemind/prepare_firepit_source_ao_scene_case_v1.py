"""Project the actual source AO intensity; retain the native sampling algorithm."""
from pathlib import Path
import json
W=Path('F:/OpenRPG_ImportWork')
context_files=[W/f'firepit-source-diffuse-context-v2/SOURCE-STATE-{i}.json' for i in range(2)]
volumes=[json.loads(json.loads(p.read_text('utf-8'))['ambient_occlusion_volume'])['MonoBehaviour'] for p in context_files]
source_intensity=volumes[0]['intensity']['m_Value'];assert source_intensity==volumes[1]['intensity']['m_Value'] and source_intensity==.5
SDK=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Lighting/ScreenSpaceLighting'
common=SDK/'GTAOCommon.hlsl';mapping=SDK/'HDRenderPipeline.AmbientOcclusion.cs'
assert 'return 1.0f - PositivePow(AO, _AOIntensity);' in common.read_text('utf-8')
assert 'settings.intensity.value,' in mapping.read_text('utf-8')
helper=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/src/shaders/source_scene_ambient_occlusion.wgsl')
helper_text=helper.read_text('utf-8')
BASE_AO=Path(__file__).with_name('prepare_firepit_ssao_native_case_v1.py')
source=BASE_AO.read_text('utf-8').replace('firepit-ssao-native-case-v1','firepit-source-ao-scene-case-v1').replace('private-ssao-visibility.glb','private-source-ao-scene.glb')
start=source.index('def diagnostic(text):');end=source.index('def main():',start)
replacement='''def diagnostic(text):
    first='let scene_ssao=textureLoad(screen_space_ambient_occlusion_texture,vec2<i32>(in.position.xy),0i).r;'
    second='source_scene_visibility=textureLoad(view_bindings::screen_space_ambient_occlusion_texture,vec2<i32>(in.frag_coord.xy),0i).r;'
    assert text.count(first)==text.count(second)==1
    for marker in [first,second]:
        lhs,rhs=marker[:-1].split('=',1)
        text=text.replace(marker,lhs+'=source_scene_ambient_visibility('+rhs+','+repr(float(source_intensity))+');')
    return text+'\\n'+helper_text
'''
source=source[:start]+replacement+source[end:]
source=source.replace('native_ssao_visibility_diagnostic=True','native_ssao_visibility_diagnostic=False,source_ao_intensity=source_intensity,source_ao_intensity_applied=True')
source=source.replace('native_ssao_fragment_visibility_diagnostic_prepared_before_pixels','source_ao_intensity_scene_projection_prepared_before_pixels')
source=source.replace('witness_alpha=.5,expected_views=2,minimum_visibility=0.0,maximum_visibility=1.0,','expected_views=2,source_ao_intensity=source_intensity,source_ao_intensity_applied=True,source_ssao_algorithm_matched=False,')
source=source.replace('pins=[Path(__file__).resolve(),','pins=[helper,common,mapping,*context_files,Path(__file__).resolve(),')
exec(compile(source,str(BASE_AO),'exec'),globals())
