"""Seal native property transfer independently from original ShaderGraph rendering."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');S=Path(__file__).resolve().parent;I=W/'gladiator-color-property-gpu-v429';O=W/'gladiator-color-transfer-seal-v430'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(x):return struct.pack('<f',float(x))
def tint(v):
    if isinstance(v,dict):
        if v.get('provenance')=='material:_Tint':yield v['literal']
        for x in v.values():yield from tint(x)
    elif isinstance(v,list):
        for x in v:yield from tint(x)
def linear(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
assert not O.exists()
run=read(I/'RUN-RESULT.v1.json');native=read(I/'NATIVE-RESULT.v1.json');prep=read(I/'PREPARED.v1.json')
assert run['unity_exit_code']==0 and not run['source_changes'] and not run['private_importer_changes']
assert sha(I/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
for p,h in prep['input_pins'].items():assert sha(p)==h
assert native['graphics_api']=='Direct3D11' and native['color_space']=='Linear' and native['source_materials_unchanged'] and len(native['materials'])==6
k=W/'gladiator-material-kernels-v419/RESULT.v1.json';kernels={r['material_guid']:r for r in read(k)['rows']};rows=[];pins={str(p):sha(p) for p in [Path(__file__),I/'RUN-RESULT.v1.json',I/'NATIVE-RESULT.v1.json',I/'PREPARED.v1.json',k,S/'run_gladiator_color_property_gpu_v428.py',S/'run_gladiator_color_property_clone_gpu_v429.py']}
for r in native['materials']:
    assert r['source_material_unchanged'] and r['negative_control_observed']
    assert r['source_serialized_before']==r['source_serialized_after']
    a=r['source_clone_get_color'];b=r['clone_get_color'];g=r['native_gpu_color']
    assert all(f32(a[c])==f32(b[c])==f32(g[c]) for c in 'rgba')
    p=W/'gladiator-active-material-ir-v386/material-family-active-ir-v1'/(r['guid']+'.json');assert sha(p)==kernels[r['guid']]['active_ir_sha256'];pins[str(p)]=sha(p)
    literals=list(tint(read(p)));assert literals
    for value in literals:
        if isinstance(value,dict):value=[value[c] for c in ('rgba' if 'r' in value else 'xyzw')]
        assert len(value)==4 and all(f32(v)==f32(g[c]) for v,c in zip(value,'rgba'))
    alternative=max(abs(linear(a[c])-g[c]) for c in 'rgb')
    rows.append(dict(guid=r['guid'],name=r['name'],gpu_color=g,source_ir_color_float32_bit_exact=True,raw_property_gpu_transfer_bit_exact=True,linearized_property_max_error=alternative,negative_control_gpu_color=r['negative_gpu_color']))
assert any(r['linearized_property_max_error']>.1 for r in rows)
O.mkdir();result=dict(status='six_native_hdr_color_property_transfers_reopened',updated_utc=datetime.now(timezone.utc).isoformat(),rows=rows,input_pins=pins,property_declarations_copied_verbatim=True,cloned_materials_only=True,source_inputs_unchanged=True,source_ir_color_needs_extra_linearization=False,original_shadergraph_render_parity_qualified=False,vault_modified=False,complete_asset_conversion_increment=0,full_import=False,next_work='Build source render contracts and compose full forward/prepass material stages, then assemble GLBs and verify product rendering.')
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — transfert des propriétés couleur mesuré, 10 octobre 2026

6 couleurs HDR _Tint mesurées sur GPU Unity à partir de clones temporaires avec les déclarations de propriétés source exactes. Les valeurs GPU correspondent bit à bit aux valeurs du graphe dérivé ; aucune linéarisation supplémentaire ne doit être ajoutée. Les six contrôles négatifs ont produit une couleur différente. Les originaux et fichiers privés sont inchangés.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-color-transfer-seal-v430\\RESULT.v1.json
Cette mesure de transfert ne qualifie pas le rendu du ShaderGraph original. Restent état et stages de rendu, GLB et vérification dans l'éditeur. Aucun nouvel asset complet qualifié.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;b=p.read_bytes();(O/(name+'.before')).write_bytes(b);p.write_bytes(note.encode('utf-8')+b)
print(json.dumps(dict(status=result['status'],materials=len(rows),extra_linearization_required=False)))
