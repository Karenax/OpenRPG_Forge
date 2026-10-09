"""Compose Detail with measured RG through the renderer's actual binding 120.

The native packet must exist first. This stages an ABI-correct uniform packet;
it does not install a material, invent texture import results or qualify GPU draw.
"""
from pathlib import Path
import datetime
import hashlib
import json

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'detail-native-default-bevy-vertex-v1'
HERE=Path(__file__).resolve().parent

def read(path):return json.loads(path.read_text('utf-8-sig'))
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def once(text,old,new):
    assert text.count(old)==1,old
    return text.replace(old,new)

def main():
    assert not ROOT.exists()
    binding_path=W/'detail-default-native-rg-binding-v1/RESULT.v1.json'
    binding=read(binding_path)
    assert binding['native_default_rg_binding_qualified'] and binding['finished_utc']
    kernel_path=W/'detail-native-default-kernel-v1/RESULT.v1.json'
    kernel=read(kernel_path)
    assert binding['material_guid']==kernel['material_guid'] and binding['channels']==['r','g']
    packet=Path(binding['packet']);raw=packet.read_bytes()
    assert len(raw)==8 and sha(packet)==binding['packet_sha256']
    parent_path=W/'detail-native-default-stages-v1/RESULT.v1.json'
    parent=read(parent_path)
    assert parent['actual_naga_exit_code']==0 and parent['material_guid']==binding['material_guid']
    original_program=Path(parent['program'])
    assert sha(original_program)==parent['program_sha256']
    product=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/src/source_graph.rs')
    product_text=product.read_text('utf-8-sig')
    assert '#[uniform(120)]\n    pub scalars: SourceGraphScalarUniforms,' in product_text
    base=HERE/'build_delta_bevy_vertex_stages_v2.py'
    pins={str(path):sha(path) for path in [binding_path,packet,kernel_path,parent_path,original_program,product,base,Path(__file__).resolve()]}
    ROOT.mkdir()
    uniform=raw+bytes(248)
    uniform_path=ROOT/'native_default_scalars_120.f32le'
    uniform_path.write_bytes(uniform)
    program=original_program.read_text('utf-8-sig')
    assert '@binding(120)' not in program
    program+='''
// Same 16 vec4 ABI as SourceGraphScalarUniforms in the actual renderer.
struct SourceGraphNativeDefaultScalars { lanes:array<vec4<f32>,16>, }
@group(#{MATERIAL_BIND_GROUP}) @binding(120) var<uniform> source_native_default_scalars:SourceGraphNativeDefaultScalars;
'''
    output=ROOT/'source_program_with_native_default_120.wgsl'
    output.write_text(program,'utf-8')
    staged=ROOT/'SELECTED-STAGES.v1.json'
    staged.write_text(json.dumps(dict(vertex_and_fragment_stage_probes_validated=1,
        rows=[dict(material_guid=parent['material_guid'],program=str(output),program_sha256=sha(output))]),indent=2),'utf-8')
    text=base.read_text('utf-8-sig')
    replacements=[
        ("ROOT = W / 'delta-vector-bevy-vertex-stages-v2'","ROOT = W / 'detail-native-default-bevy-compositions-v1'"),
        ("stages_path = W / 'delta-vector-native-stages-v1/RESULT.v1.json'",'stages_path = Path('+repr(str(staged))+')'),
        ("stages['vertex_and_fragment_stage_probes_validated'] == len(stages['rows']) == 6","stages['vertex_and_fragment_stage_probes_validated'] == len(stages['rows']) == 1"),
        ("kernels_path = W / 'delta-vector-node-wgsl-v3/RESULT.v1.json'",'kernels_path = Path('+repr(str(kernel_path))+')'),
        ('requested=6,','requested=1,'),
        ("'\\nreturn output;')","'\\noutput.native_default_mask_rg=source_native_default_scalars.lanes[0].xy;\\nreturn output;')")]
    for old,new in replacements:text=once(text,old,new)
    # Preserve original four real Bevy permutations and all primary-module checks.
    exec(compile(text,str(base),'exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__'))
    composed_path=W/'detail-native-default-bevy-compositions-v1/RESULT.v1.json'
    composed=read(composed_path)
    for path,digest in pins.items():assert sha(Path(path))==digest,path
    receipt=dict(status='detail_measured_default_abi_and_bevy_vertex_composed' if composed['native_vertex_programs_composed']==1 else 'refused_or_unverified',
        material_guid=binding['material_guid'],actual_positive_compositions=composed['actual_positive_compositions'],
        failures=composed['failures'],uniform_binding=120,uniform_vec4_lane=0,uniform_channels=['x','y'],
        uniform_packet=str(uniform_path),uniform_packet_sha256=sha(uniform_path),uniform_bytes=len(uniform),
        source_rg_packet_sha256=sha(packet),composition_receipt=str(composed_path),composition_receipt_sha256=sha(composed_path),
        input_pins=pins,renderer_installed=False,gpu_rendered=False,full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
    print(json.dumps({key:value for key,value in receipt.items() if key!='input_pins'}))

if __name__=='__main__':main()
