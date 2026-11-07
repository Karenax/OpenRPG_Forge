"""Publish a bounded checkpoint without promoting shader validation to asset admission."""
from pathlib import Path
import hashlib,json,datetime
W=Path('F:/OpenRPG_ImportWork')
O=W/'pirate-palm-scalar-repair-delivery-v544'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
repairpath=W/'pirate-palm-wgsl-v543/DYNAMIC-SCALAR-REPAIR.v1.json'
repair=read(repairpath)
nativepath=W/'pirate-palm-wind-native-reference-v541/RUN-RESULT.v1.json'
native=read(nativepath)
shaderresult=read(W/'pirate-palm-wgsl-v543/RESULT.v1.json')
batch=read(W/'pirate-palm-wgsl-input-v543/BATCH.v1.json')
assert repair['source_ir_unchanged'] and repair['source_scalar']==3
assert native['unity_exit_code']==0 and native['native_wind_samples']==3192
assert native['source_changes']==native['private_importer_changes']==[]
assert shaderresult['failed_materials']==0 and shaderresult['rows'][0]['actual_naga_exit_code']==0
assert sha(shaderresult['rows'][0]['wgsl'])==repair['shader_sha256']
for p,h in batch['source_pins'].items():assert sha(p)==h,p
inputpath=W/'pirate-palm-wind-native-reference-v541/INPUT.f32'
outputpath=W/'pirate-palm-wind-native-reference-v541/native-output.f32'
assert inputpath.stat().st_size==3192*96 and outputpath.stat().st_size==3192*48
O.mkdir()
pins={str(p):sha(p) for p in [Path(__file__),repairpath,nativepath,inputpath,outputpath,Path(shaderresult['rows'][0]['wgsl'])]}
result=dict(status='native_wind_capture_and_source_scalar_repair_sealed_gpu_comparison_pending',
 utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),pins=pins,native_samples=3192,
 native_reference_backend='Direct3D11',repaired_source_scalar=3,previous_emitted_scalar=0,
 naga_exit_code=0,new_fully_converted_assets=0,full_import=False,studio_verified=False,
 clarification='v543 DYNAMIC-SCALAR-REPAIR receipt supersedes inherited v538 generator wording about unchanged node equations. Only scalar dictionary decoding is repaired; the source IR and purchased sources remain unchanged.',
 next='Execute matching WGSL compute on the same frozen v541 inputs; compare position, wind delta, normal and tangent with tolerance fixed before execution. Then qualify material sampling, rendering and editor access.')
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''# Reprise — lot palmiers v541 / v543 / v544

Unity D3D11 : 3 192 échantillons natifs capturés, sortie finie, sources inchangées.
Défaut identifié et corrigé dans le dérivé WGSL : constante dynamique e00=3 lue précédemment comme zéro.
WGSL corrigé : Naga PASS. Les valeurs dynamiques vectorielles/matricielles non qualifiées sont refusées.
La formulation héritée « all node equations unchanged » du générateur v543 décrit l'ancien v538 ; elle est remplacée pour cette réparation par DYNAMIC-SCALAR-REPAIR.v1.json.
Preuve : F:\\OpenRPG_ImportWork\\pirate-palm-scalar-repair-delivery-v544\\RESULT.v1.json.
Prochaine étape : comparaison GPU WGSL avec INPUT.f32 de v541, puis matériaux/rendu et accès éditeur.
Aucun nouvel asset intégralement converti. Six variantes de palmiers restent non admises. Images primaires manquantes : dernier lot vérifié 638 ; aucun changement de catalogue dans ce lot.

'''
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name
    old=p.read_bytes()
    (O/(name+'.before')).write_bytes(old)
    p.write_text(note+old.decode('utf-8-sig'),'utf-8')
print(json.dumps(result))
