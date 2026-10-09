"""Five interleaved surface diagnostics with source debug encoding and model/ground masks."""
from pathlib import Path
import json
W=Path('F:/OpenRPG_ImportWork');BASE_SURFACE=Path(__file__).with_name('prepare_firepit_ssao_native_case_v1.py')
HDRP=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime'
sdk=[HDRP/'Debug/DebugDisplayMaterial.hlsl',HDRP/'Material/Lit/Lit.cs.hlsl',HDRP/'Material/Lit/Lit.cs']
assert 'result = SRGBToLinear(max(0, result));' in sdk[0].read_text('utf-8')
assert 'result = surfacedata.baseColor;' in sdk[1].read_text('utf-8')
roles=['Albedo','Normal','Smoothness','AmbientOcclusion','Metal'];source_root=W/'firepit-source-surface-debug-v1'
run=json.loads((source_root/'RUN-RESULT.v1.json').read_text('utf-8'));assert run['actual_exit_code']==0
refs=[dict(role=role,path=str(source_root/f'DEBUG-{role}.rgba16f'),sha256=run['debug_files'][str(source_root/f'DEBUG-{role}.rgba16f')]) for role in roles]
source=BASE_SURFACE.read_text('utf-8').replace("OLD=W/'firepit-specular-scene-case-v2'","OLD=W/'firepit-source-ao-scene-case-v1'").replace('firepit-ssao-native-case-v1','firepit-surface-native-case-v1').replace('private-ssao-visibility.glb','private-surface-diagnostic.glb')
start=source.index('def diagnostic(text):');end=source.index('def main():',start)
witness='''let surface_mode=(u32(in.position.x)+512u*u32(in.position.y))%5u;
 var surface_value=surface.base_color;
 switch surface_mode {
  case 1u:{let n=normalize((source_graph.source_world_from_target_world*vec4<f32>(surface.normal_world,0.0)).xyz);surface_value=source_surface_debug_linear_from_srgb(n*0.5+vec3<f32>(0.5));}
  case 2u:{surface_value=source_surface_debug_linear_from_srgb(vec3<f32>(1.0-surface.perceptual_roughness));}
  case 3u:{surface_value=source_surface_debug_linear_from_srgb(vec3<f32>(surface.diffuse_occlusion));}
  case 4u:{surface_value=source_surface_debug_linear_from_srgb(vec3<f32>(surface.metallic));}
  default:{}
 }
 out.color=vec4<f32>(surface_value,0.5);'''
codec='''
fn source_surface_debug_linear_from_srgb(value:vec3<f32>)->vec3<f32> {
 let c=max(value,vec3<f32>(0.0));
 return select(c/12.92,pow((c+vec3<f32>(0.055))/1.055,vec3<f32>(2.4)),c>vec3<f32>(0.04045));
}
'''
replacement="def diagnostic(text):\n    marker='out.color=source_graph_native_light_transport(p,surface);';assert text.count(marker)==1\n    return text.replace(marker,"+repr(witness)+")+"+repr(codec)+"\n"
source=source[:start]+replacement+source[end:]
source=source.replace("text=diagnostic(text) if name=='forward' else text","text=diagnostic(text).replace('out.color=vec4<f32>(surface_value,0.5);','out.color=vec4<f32>(surface_value,0.25);') if name=='forward' else text")
source=source.replace('native_ssao_visibility_diagnostic=True','native_ssao_visibility_diagnostic=False,source_surface_debug_refs=refs')
source=source.replace('native_ssao_fragment_visibility_diagnostic_prepared_before_pixels','five_source_surface_debug_encodings_native_diagnostic_prepared_before_pixels')
source=source.replace('witness_alpha=.5,expected_views=2,minimum_visibility=0.0,maximum_visibility=1.0,','model_alpha=.5,receiver_alpha=.25,expected_views=2,source_surface_debug_refs=refs,maximum_encoded_surface_mae=2/255,source_debug_encoding_interpreted=True,')
source=source.replace('pins=[Path(__file__).resolve(),','pins=[*sdk,source_root/\'RUN-RESULT.v1.json\',*[Path(v[\'path\']) for v in refs],Path(__file__).resolve(),')
exec(compile(source,str(BASE_SURFACE),'exec'),globals())
