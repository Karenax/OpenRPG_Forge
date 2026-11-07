"""Independently reopen material defaults; do not claim render equivalence."""
from pathlib import Path
import hashlib,json
from datetime import datetime,timezone

W=Path('F:/OpenRPG_ImportWork')
I=W/'gladiator-native-shader-defaults-v410'
O=W/'gladiator-material-defaults-seal-v411'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
run=read(I/'RUN-RESULT.v1.json');native=read(I/'NATIVE-RESULT.v1.json')
assert run['unity_exit_code']==0 and run['source_inputs_unchanged']
assert run['source_changes']==[] and run['private_importer_changes']==[]
assert sha(I/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
assert native['source_materials_unchanged'] and len(native['materials'])==6
pins=read(I/'PREPARED.v1.json')['input_pins']
for p,h in pins.items():assert sha(Path(p))==h,p
shader=(I/'source-generated.shader').read_text('utf-8')
assert '_ALPHATOMASK_ON' not in shader
assert 'AlphaToMask [_AlphaCutoffEnable]' in shader
defaults={'_DstBlend2':0,'_ExcludeFromTUAndAA':0,'_MaterialTypeMask':2,'_PerPixelSorting':0}
rows=[]
for m in native['materials']:
    assert m['source_material_unchanged']
    assert json.loads(m['read_probe_before_json'])==json.loads(m['read_probe_after_json'])
    assert not m['alpha_to_mask_keyword_declared']
    assert '_ALPHATOMASK_ON' not in m['declared_keywords']
    props={p['name']:p for p in m['properties']}
    assert len(props)==len(m['properties'])
    for k,v in defaults.items():
        assert props[k]['type']=='Float'
        assert props[k]['value_float']==v and props[k]['default_float']==v
    rows.append(dict(guid=m['guid'],name=m['name'],sdk_added_properties_match_actual_shader_defaults=defaults,temporary_clone_unchanged_during_queries=True,alpha_to_mask_keyword_declared=False,alpha_to_coverage_render_state_present=True))
inputs=[I/'NATIVE-RESULT.v1.json',I/'RUN-RESULT.v1.json',I/'PREPARED.v1.json',I/'source-generated.shader',Path(__file__)]
O.mkdir()
result=dict(status='six_material_shader_defaults_reopened_and_sealed',updated_utc=datetime.now(timezone.utc).isoformat(),rows=rows,input_pins={str(p):sha(p) for p in inputs},source_input_pin_count=len(pins),source_inputs_unchanged=True,vault_modified=False,material_gpu_qualified=False,original_sdk_render_equivalence_qualified=False,full_import=False,complete_asset_conversion_increment=0,next_work='Export imported texture storage and qualify opacity/normal/color sampling before GLB material conversion.')
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — valeurs des shaders vérifiées, 10 octobre 2026

Les six matériaux ont été réouverts dans Unity 6000.5.8f1. Les quatre propriétés ajoutées par le SDK correspondent exactement aux valeurs par défaut du shader généré : _DstBlend2=0, _ExcludeFromTUAndAA=0, _MaterialTypeMask=2, _PerPixelSorting=0. La lecture utilise une copie temporaire ; les originaux et les fichiers privés sur disque sont inchangés.

Le mot-clé _ALPHATOMASK_ON est absent de l'espace des mots-clés et du shader généré actuel. Le shader contient néanmoins AlphaToMask [_AlphaCutoffEnable] : ceci ne prouve pas une équivalence de rendu avec l'ancien SDK.

Preuves : F:\\OpenRPG_ImportWork\\gladiator-material-defaults-seal-v411\\RESULT.v1.json
Géométrie déjà vérifiée : 17 LOD de 5 prefabs. Aucun nouvel asset complet qualifié. Restent textures importées, fidélité des matériaux, GLB et vérification dans l'éditeur.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name
    original=p.read_bytes();(O/(name+'.before')).write_bytes(original)
    p.write_bytes(note.encode('utf-8')+original)
print(json.dumps(dict(status=result['status'],materials=len(rows),complete_asset_conversion_increment=0)))
