"""Pin the real pass/caster boundary before adding legacy material admission."""
from pathlib import Path
import datetime
import hashlib
import json
import re

R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'delta-firepit-single-pass-requirements-v1'
SHADER=W/'unity-builtin-shaders-6000.5.8f1-v2/source/builtin/DefaultResourcesExtra/Particle Alpha Blend.shader'
NATIVE=W/'unity-delta-firepit-effect-materials-v1/MATERIALS.v1.json'
IR=W/'delta-complete-prefab-source-reopen-v1'
BEVY=Path('C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/bevy_pbr-0.19.1/src')

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return json.loads(p.read_text('utf-8-sig'))

def main():
    assert not ROOT.exists()
    proof=read(IR/'RESULT.v1.json'); pins=dict(proof['input_pins']); pins.update(proof['output_pins'])
    files=[Path(__file__).resolve(),SHADER,NATIVE,IR/'RESULT.v1.json',
        R/'crates/forge_assets/src/source_graph_bundle.rs',R/'crates/forge_engine/src/source_graph_program.rs',
        BEVY/'extended_material.rs',BEVY/'prepass/mod.rs']
    for p in files: pins[str(p)]=sha(p)
    for p,digest in pins.items(): assert sha(Path(p))==digest,'Changed source: '+p
    shader=SHADER.read_text('utf-8-sig')
    assert len(re.findall(r'^\s*Pass\s*(?:\{|$)',shader,re.M))==1
    assert not re.search(r'^\s*(?:Fallback|UsePass)\b',shader,re.I|re.M)
    assert re.search(r'ColorMask\s+RGB\b',shader) and re.search(r'ZWrite\s+Off\b',shader)
    material_cases=read(NATIVE)['cases']; assert len(material_cases)==2
    rows=[]
    for case in material_cases:
        assert case['shader_name']=='Legacy Shaders/Particles/Alpha Blended' and case['shader_file_id']=='203'
        assert case['pass_names']==['<Unnamed Pass 0>']
        p=next(p for p in IR.glob('*-SOURCE-IR.v1.json') if read(p)['material_guid']==case['material_guid'])
        source=read(p); renderer=source['native_hierarchy_renderer']
        assert renderer['type']=='UnityEngine.ParticleSystemRenderer' and renderer['enabled']
        assert renderer['shadow_casting_mode']==0 and not renderer['receive_shadows']
        assert not renderer['static_mesh_adapter_applicable']
        rows.append(dict(material_guid=case['material_guid'],node_path=source['native_node_path'],
            original_pass_names=case['pass_names'],shadow_casting_mode=0,receive_shadows=False,
            static_mesh_adapter_applicable=False,original_material_documents_retained=True))
    wire=(R/'crates/forge_assets/src/source_graph_bundle.rs').read_text('utf-8')
    program=(R/'crates/forge_engine/src/source_graph_program.rs').read_text('utf-8')
    assert 'pub prepass: SourceGraphBlob' in wire and 'pub prepass_index:usize' in program
    api=(BEVY/'extended_material.rs').read_text('utf-8')
    assert 'fn enable_prepass() -> bool' in api and 'fn enable_shadows() -> bool' in api
    for p,digest in pins.items(): assert sha(Path(p))==digest,'Source changed during review: '+p
    ROOT.mkdir()
    result=dict(status='two_original_single_pass_no_shadow_particle_renderers_verified',input_pins=pins,
        rows=rows,source_shader_pass_count=1,source_shader_fallback_or_usepass=False,
        current_source_graph_contract_requires_prepass=True,
        admission_requirement='Generic explicit forward-only material capability; no invented source prepass, no shadow caster, original RGB mask and source billboard buffers. Preserve Graph material routing independently.',
        bevy_capabilities_static_per_material_type=True,
        shader_gpu_half_comparison_required=True,full_particle_simulation_required=True,
        new_material_type_implemented=False,renderer_installed=False,whole_effect_qualified=False,full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(renderers_verified=2,source_passes=1,shadow_casting=False,renderer_installed=False,full_import=False)))

if __name__=='__main__': main()
