"""Prepare 756 source SDK comparisons before reading any target pixels."""
from pathlib import Path
import json
W=Path('F:/OpenRPG_ImportWork')
ENGINE=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/src/shaders')
BASE=Path(__file__).with_name('prepare_firepit_diffuse_probe_gpu_case_v1.py')
sdk_path=W/'firepit-source-specular-sdk-reopen-v2/RESULT.v1.json'
sdk=json.loads(sdk_path.read_text('utf-8'));assert sdk['status']=='native_source_deferred_specular_sdk_756_cases_live_context_verified'
rows=sdk['rows'];assert len(rows)==756
normals=[rows[i*252]['normal'] for i in range(3)]
views=[rows[i*84]['view'] for i in range(3)]
rough=[rows[i*12]['authored_perceptual_roughness'] for i in range(7)]
visibility=[rows[i*4]['scene_visibility'] for i in range(3)]
colors=[rows[i*2]['fresnel0'] for i in range(2)]
ao=[rows[i]['material_ao'] for i in range(2)]
for i,c in enumerate(rows):
    assert c['normal']==normals[i//252] and c['view']==views[(i//84)%3]
    assert c['authored_perceptual_roughness']==rough[(i//12)%7]
    assert c['scene_visibility']==visibility[(i//4)%3] and c['fresnel0']==colors[(i//2)%2] and c['material_ao']==ao[i%2]
    assert c['source_gpu']['reflected_light_f90'][3]==1.0
def vectors(values):return ','.join('vec3<f32>('+','.join(repr(float(v)) for v in row)+')' for row in values)
def scalars(values):return ','.join(repr(float(v)) for v in values)
witness=f'''let witness_index=(u32(in.position.x)+512u*u32(in.position.y))%756u;
let normals=array<vec3<f32>,3>({vectors(normals)});
let views=array<vec3<f32>,3>({vectors(views)});
let roughness=array<f32,7>({scalars(rough)});
let visibility=array<f32,3>({scalars(visibility)});
let colors=array<vec3<f32>,2>({vectors(colors)});
let material_ao=array<f32,2>({scalars(ao)});
let original_normal=normalize(normals[witness_index/252u]);
let original_view=normalize(views[(witness_index/84u)%3u]);
let r=roughness[(witness_index/12u)%7u];
let original_specular_ao=source_deferred_specular_ao(max(dot(original_normal,original_view),0.0001),material_ao[witness_index%2u],r*r);
let reflected=source_deferred_sky_specular(original_normal,original_view,r,colors[(witness_index/2u)%2u],original_specular_ao,visibility[(witness_index/4u)%3u],1.0,1.0);
out.color=vec4<f32>(reflected,0.5);'''
helpers=[ENGINE/n for n in ['source_sky_cube.wgsl','source_preintegrated_brdf.wgsl','source_deferred_diffuse.wgsl','source_deferred_specular.wgsl']]
helper_text='\n'.join(p.read_text('utf-8') for p in helpers)
source=BASE.read_text('utf-8').replace('firepit-diffuse-probe-gpu-case-v1','firepit-specular-gpu-case-v1')
source=source.replace('private-six-direction-witness.glb','private-specular-witness.glb')
source=source.replace("helper=ENGINE/'shaders/source_diffuse_probe.wgsl';helper_text=helper.read_text('utf-8')",'helper=Path(__file__).resolve();helper_text='+repr(helper_text))
start=source.index("    witness='");end=source.index('\n',start);source=source[:start]+'    witness='+repr(witness)+source[end:]
source=source.replace('private_diffuse_probe_witness','private_source_specular_witness')
source=source.replace("    (ROOT/'CASE.v1.json').write_text",
    "    case['specular_sdk_protocol']=read(W/'firepit-source-specular-sdk-reopen-v2/RESULT.v1.json')\n"
    "    case['sky_cube_packet']=read(W/'source-sky-cube-packet-v1/PACKET.v1.json')\n"
    "    case['brdf_lookup_protocol']=read(W/'source-brdf-lookup-case-v1/CASE.v1.json')\n"
    "    (ROOT/'CASE.v1.json').write_text")
source=source.replace('six_direction_gpu_witness_prepared_before_pixels','source_deferred_specular_756_case_gpu_witness_prepared_before_pixels')
source=source.replace('expected_directions=6','expected_cases=756')
source=source.replace('minimum_samples_per_direction=100','minimum_samples_per_case=8')
source=source.replace('absolute_channel_error_floor=0.0001','absolute_channel_error_floor=0.001')
source=source.replace('mathematical_irradiance_only=True','complete_source_specular_composition_only=True,source_direction_basis_explicit=True')
extra=[sdk_path,W/'source-sky-cube-packet-v1/PACKET.v1.json',W/'source-brdf-lookup-case-v1/CASE.v1.json',*helpers]
source=source.replace('case_path,fixture,environment,audit,','case_path,fixture,environment,audit,'+','.join('Path('+repr(str(p))+')' for p in extra)+',')
exec(compile(source,str(BASE),'exec'),globals())
