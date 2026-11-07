"""Seal reopened source contracts and wind topology while retaining WGSL refusal."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-palm-contract-and-ir-delivery-v533'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
cp=W/'pirate-palm-render-contract-v529/RESULT.v1.json';ip=W/'pirate-palm-active-material-ir-v531/RESULT.v1.json';wp=W/'pirate-palm-wgsl-v532/RESULT.v1.json'
c,i,w=[read(p) for p in [cp,ip,wp]]
assert c['status']=='authored_palm_properties_and_native_generated_pass_contract_reopened'
assert c['generated_pass_count']==17 and len(c['authored_property_checks'])==71
assert len(c['explicit_unqualified_stencil_changes'])==3
assert i['status']=='palm_active_material_topology_compiled' and i['actual_exit_code']==0
assert i['nodes']==36 and len(i['terminals'])==13
assert i['opcode_counts']['TimeNode']==i['opcode_counts']['GradientNoiseNode']==1
assert w['requested_materials']==w['failed_materials']==1 and w['naga_validated_materials']==0
assert w['failures'][0]['error']=="'guid'"
orm=next(p for p in c['native_material']['properties'] if p['name']=='_ORMMap')
assert orm['texture_null'] and orm['default_texture_name']=='white'
pins={}
for proof,field in [(c,'input_pins'),(i,'input_pins'),(w,'source_pins')]:
    for p,h in proof[field].items():
        if p in pins:assert pins[p]==h,p
        pins[p]=h
for p,h in pins.items():assert sha(p)==h,p
assert sha(i['ir_path'])==i['ir_sha256'];pins[i['ir_path']]=i['ir_sha256']
for p in [Path(__file__),cp,ip,wp]:pins[str(p)]=sha(p)
O.mkdir()
result=dict(status='palm_native_contract_and_wind_topology_verified_wgsl_default_binding_refusal_retained',authored_properties_checked=71,generated_passes=17,topology_nodes=36,source_terminals=13,wind_time_and_noise_preserved=True,unqualified_stencil_changes=c['explicit_unqualified_stencil_changes'],wgsl_refusal=w['failures'],native_orm_default=orm,missing_graph_default_texture_guid='34307d11fc1ff8d46a03b56ec522f4a7',next_required_evidence='Capture native SDK default bytes and verify the effective texture binding before adapting WGSL generation; preserve the original unresolved texture reference.',six_palm_lod_variants_still_refused=True,input_pins=pins,thumbnail_admission_qualified=False,source_program_gpu_qualified=False,full_import=False,complete_asset_conversion_increment=0,vault_mutated=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='## Contrat palmiers et vent - v528 a v533\n\nExport natif termine, exit Unity 0, aucune mutation source/privee. Contrat : 71 proprietes controlees, 17 passes, alpha-to-coverage explicite DepthOnly/MotionVectors ; trois changements stencil conserves non qualifies. IR original : 36 operations, 13 sorties dont BentNormal, temps et bruit du vent preserves. Generation WGSL v532 refusee sur le binding ORM : GUID de texture par defaut absent du pack, texture native null et default_texture_name white. Capture des bytes SDK natifs lancee v535 ; verifier son etat avant relance. Ne pas relancer v528. Audit des helpers pour prochains lots : v535 une fois prepare, sinon v528. Les six variantes palmiers restent refusees ; pas de nouvelle conversion complete. Preuve : F:/OpenRPG_ImportWork/pirate-palm-contract-and-ir-delivery-v533/RESULT.v1.json.\n\n'
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;raw=p.read_bytes();(O/(name+'.before')).write_bytes(raw);p.write_text(note+raw.decode('utf-8-sig'),'utf-8')
print(json.dumps(dict(status=result['status'],source_terminals=13,topology_nodes=36,palm_admission=False)))
