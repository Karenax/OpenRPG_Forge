"""Measure output-liveness effects without admitting an instrumentation-only pass."""
from pathlib import Path
import hashlib,json,struct,datetime
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-palm-gradient-liveness-v564'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
paths=[W/'pirate-palm-wind-gpu-comparison-v559/RESULT.v1.json',W/'pirate-palm-wind-gpu-comparison-v563/RESULT.v1.json']
instrumented,complete=map(read,paths)
assert instrumented['failed_components']==0 and complete['failed_components']==1
for r in [instrumented,complete]:
    for p,h in r['pins'].items():assert sha(p)==h,p
files=[W/'pirate-palm-wind-native-reference-v541/native-output.f32',W/'pirate-palm-wind-native-reference-v558/native-output.f32',W/'pirate-palm-wind-gpu-comparison-v559/wgsl-output.f32',W/'pirate-palm-wind-gpu-comparison-v563/wgsl-output.f32']
arrays=[struct.unpack('<38304f',p.read_bytes()) for p in files]
native,detail_native,detail_wgsl,full_wgsl=arrays
comparisons={}
for label,a,ai,b,bi in [('native_position_output_liveness',native,0,detail_native,10),('native_wind_output_liveness',native,3,detail_native,11),('wgsl_position_output_liveness',full_wgsl,0,detail_wgsl,10),('wgsl_wind_output_liveness',full_wgsl,3,detail_wgsl,11)]:
    changes=[dict(sample=i,original=a[i*12+ai],instrumented=b[i*12+bi],error=abs(a[i*12+ai]-b[i*12+bi])) for i in range(3192) if a[i*12+ai]!=b[i*12+bi]]
    comparisons[label]=dict(changed_samples=len(changes),max_error=max([r['error'] for r in changes],default=0),examples=changes[:8])
O.mkdir()
result=dict(status='instrumentation_pass_kept_distinct_from_full_vertex_failure',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),samples=3192,
instrumented_failed_components=0,complete_vertex_failed_components=1,comparisons=comparisons,
pins={str(p):sha(p) for p in paths+files+[Path(__file__)]},tolerance_absolute=1e-5,full_import=False,new_admitted_assets=0,
next='Inspect compiler/backend normalization and arithmetic under complete vertex output liveness; maintain the refused palm variants. Other imagery/conversion lots remain independently actionable.')
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''# Reprise — palmiers : gradients et arrondi v558 à v564

Sonde détaillée : quatre gradients, interpolant et position passent sur 3 192 entrées, sans relever la tolérance absolue 1e-5.
Vertex complet : un seul écart de position X subsiste (échantillon 537, 0,0000305176). Les essais d'interpolation fusionnée, coordonnées fusionnées et dimensions scalaires ne l'ont pas supprimé.
Le PASS de la sonde instrumentée ne vaut pas qualification du vertex complet. Les différences liées aux sorties conservées sont mesurées dans la preuve v564.
Preuve : F:\\OpenRPG_ImportWork\\pirate-palm-gradient-liveness-v564\\RESULT.v1.json.
Prochaine étape palmiers : compilation/normalisation sous le vertex complet. Les autres lots d'images et de conversion restent réalisables indépendamment.
Sources intactes ; six variantes palmiers refusées ; aucun nouvel asset totalement converti.

'''
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;old=p.read_bytes();(O/(name+'.before')).write_bytes(old);p.write_text(note+old.decode('utf-8-sig'),'utf-8')
print(json.dumps(result['comparisons']))
