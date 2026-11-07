"""Seal actual Naga validation and native SDK binding without claiming render parity."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-palm-wgsl-delivery-v539'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
bp=W/'pirate-palm-native-default-binding-v536/RESULT.v1.json';wp=W/'pirate-palm-wgsl-v538/RESULT.v1.json';ip=W/'pirate-palm-active-material-ir-v531/RESULT.v1.json'
b,w,i=[read(p) for p in [bp,wp,ip]]
assert w['status']=='source_material_wgsl_kernels_naga_validated'
assert w['requested_materials']==w['naga_validated_materials']==len(w['rows'])==1 and not w['failures']
r=w['rows'][0];assert r['actual_naga_exit_code']==0 and sha(r['wgsl'])==r['wgsl_sha256']
assert r['active_ir_sha256']==b['effective_ir_sha256'] and sha(b['effective_ir_path'])==b['effective_ir_sha256']
assert sha(b['resource']['path'])==b['resource']['sha256']
assert i['nodes']==36 and len(i['terminals'])==13 and i['opcode_counts']['TimeNode']==i['opcode_counts']['GradientNoiseNode']==1
assert len(r['terminal_fields'])==13
native_resources=[t for t in r['source_texture_bindings'] if t.get('kind')=='measured_native_sdk_default']
assert len(native_resources)==1 and native_resources[0]['resource_key']==b['resource']['key']
pins={}
for proof,field in [(w,'source_pins'),(b,'input_pins'),(i,'input_pins')]:
    for p,h in proof[field].items():
        if p in pins:assert pins[p]==h,p
        pins[p]=h
for p,h in pins.items():assert sha(p)==h,p
for p in [Path(__file__),bp,wp,ip,Path(r['wgsl']),Path(b['effective_ir_path']),Path(b['resource']['path'])]:pins[str(p)]=sha(p)
for name in ['naga.stdout.log','naga.stderr.log']:
    p=Path(r['wgsl']).parent/name;pins[str(p)]=sha(p)
O.mkdir()
result=dict(status='palm_wind_wgsl_naga_validation_and_measured_sdk_binding_sealed',shader=r,source_nodes=36,source_terminals=13,native_sdk_resource=b['resource'],original_missing_graph_reference=b['original_missing_reference'],native_binding_is_explicit_overlay=True,original_sources_preserved=True,input_pins=pins,actual_naga_exit_code=0,source_program_gpu_qualified=False,source_version_render_equivalence_qualified=False,material_stencil_equivalence_qualified=False,geometry_conversion_qualified=False,studio_gui_verified=False,thumbnail_admission_qualified=False,full_import=False,complete_asset_conversion_increment=0,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='## Shader palmiers WGSL valide - v535 a v539\n\nDefaut SDK white capture : 4x4 RGBA32, R8G8B8A8_SRGB, un mip, 64 bytes controles. Binding ORM natif conserve en overlay explicite ; reference de texture manquante originale preservee. Shader WGSL des frondes genere et Naga exit 0 : 36 operations, 13 sorties, temps et bruit du vent preserves. Trois bindings texture dont un SDK mesure ; aucun remplacement de texture artiste. Prochain lot : comparaison GPU des equations actives/vent/sampling avec Unity, puis composition aux passes Bevy et verification des ecarts stencil. Les six variantes palmiers restent refusees pour admission des images. Ne pas relancer les captures v528/v535. Dernier audit helpers Unity : F:/OpenRPG_ImportWork/pirate-palm-native-contract-v535/HELPER-OWNERSHIP.v1.json (19 helpers controles). Aucun job courant connu apres ces captures terminales. Preuve : F:/OpenRPG_ImportWork/pirate-palm-wgsl-delivery-v539/RESULT.v1.json. Images principales manquantes : 638 ; conversion complete et GUI encore non qualifies.\n\n'
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;raw=p.read_bytes();(O/(name+'.before')).write_bytes(raw);p.write_text(note+raw.decode('utf-8-sig'),'utf-8')
print(json.dumps(dict(status=result['status'],source_terminals=13,naga_exit=0,source_gpu_qualified=False)))
