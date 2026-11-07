"""Record an observed texture-contract mismatch without inventing a repair."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-speedtree-binding-review-v923'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def main():
    visual_path=W/'mountain-capture-agent-review-v898/RESULT.v1.json';visual=read(visual_path)
    proof_path=W/'mountain-exact-capture-review-v897/RESULT.v1.json';proof=read(proof_path)
    shader=W/'mountain-hdrp-capture-stage-v869/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime/Material/Nature/SpeedTree8.shadergraph'
    meta=Path(str(shader)+'.meta');guid='4819724840ee9444f9da841b477038ce';assert 'guid: '+guid in meta.read_text('utf-8-sig')
    decoder=json.JSONDecoder();text=shader.read_text('utf-8-sig');objects=[]
    while text.strip():
        text=text.lstrip();obj,end=decoder.raw_decode(text);objects.append(obj);text=text[end:]
    extra=[obj for obj in objects if obj.get('m_OverrideReferenceName')=='_ExtraTex'];assert len(extra)==1
    assert extra[0]['m_Name']=='Smoothness (R), Metallic (G), AO (B)'
    materials={};pins={str(p):sha(p) for p in [visual_path,proof_path,shader,meta]}
    for row in proof['source_material_changes']:
        path=Path(row['source']);m=Path(str(path)+'.meta');material_guid=re.search(r'^guid: (\w+)$',m.read_text('utf-8-sig'),re.M)[1]
        materials[material_guid]=row
    cards={r['id']:r for r in proof['rows']};observations=[]
    for held in visual['visual_holds']:
        receipt=Path(cards[held['id']]['native_receipt']);native=read(receipt);pins[str(receipt)]=sha(receipt)
        for renderer in native['renderers']:
            for material_id,shader_id in zip(renderer['materials'],renderer['shaders']):
                if shader_id.split(':')[0]!=guid:continue
                material=materials[material_id.split(':')[0]];p=Path(material['source']);assert sha(p)==material['original_sha256'];pins[str(p)]=sha(p)
                serialized=p.read_text('utf-8-sig')
                enabled=re.search(r'^    - EFFECT_EXTRA_TEX: (\S+)$',serialized,re.M)
                extra_binding=re.search(r'^    - _ExtraTex:\s*\n        m_Texture: (.*)$',serialized,re.M)
                binding=re.search(r'^    - _OcclusionMap:\s*\n        m_Texture: \{fileID: 2800000, guid: ([0-9a-f]{32}), type: 3\}',serialized,re.M)
                observations.append(dict(id=held['id'],material_source=str(p),shader_guid=guid,extra_texture_feature_serialized_value=float(enabled[1]) if enabled else None,installed_shader_extra_property='_ExtraTex',installed_shader_channel_contract=extra[0]['m_Name'],source_extra_tex_binding_absent=extra_binding is None,source_extra_tex_binding=extra_binding[1] if extra_binding else None,source_occlusion_map_guid=binding[1] if binding else None,diagnosis='Texture bindings inventoried against installed SDK contract; native defaults and old SDK semantics still require measurement.'))
    assert {r['id'] for r in observations}=={r['id'] for r in visual['visual_holds']}
    O.mkdir(exist_ok=False);result=dict(status='three_held_tree_speedtree_texture_contracts_inspected',finished_utc=datetime.now(timezone.utc).isoformat(),observations=observations,input_pins=pins,automatic_texture_remapping_authorized_by_evidence=False,root_cause_qualified=False,source_mutations=0,images_admitted=0,full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(status=result['status'],held_cards=3,root_cause_qualified=False)))
if __name__=='__main__':main()
