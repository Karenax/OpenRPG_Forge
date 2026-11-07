"""Retain the failed implicit-sampling qualification, with precise lot status."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');C=W/'house-hdrp-bias-surface-gpu-comparison-v624';N=W/'house-hdrp-bias-surface-reference-v623';O=W/'house-implicit-sampling-outcome-v627'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
r=read(C/'RESULT.v1.json');n=read(N/'RUN-RESULT.v1.json');rows=read(C/'PLANE-COMPARISONS.v1.json')
assert r['status']=='house_implicit_surface_gpu_mismatches_retained' and r['actual_gpu_exit_code']==0
assert n['unity_exit_code']==0 and not n['source_changes'] and not n['private_importer_changes']
assert r['failed_components']==3206 and r['orientation_marker_failed_components']==0
pins={}
for key in ['input_pins','output_pins']:
    for p,h in r[key].items():assert sha(p)==h,p;pins[p]=h
failed=[row for row in rows if row['failed_components']]
assert len(rows)==288 and len(failed)==4 and all(row['plane']==0 for row in failed)
previous=W/'house-implicit-surface-gpu-comparison-v622/MISMATCHES.v1.json'
identical=read(previous)==read(C/'MISMATCHES.v1.json');assert identical
for p in [Path(__file__),C/'RESULT.v1.json',N/'RUN-RESULT.v1.json',previous]:pins[str(p)]=sha(p)
O.mkdir()
result=dict(status='house_implicit_sampling_qualification_failed_evidence_retained',utc=datetime.now(timezone.utc).isoformat(),lot='L03.HOUSE.MAIN.01',materials=6,prefab_cards=24,frames=48,planes=288,passed_planes=284,failed_planes=4,failed_components=3206,orientation_marker_failed_components=0,max_absolute_error=r['max_absolute_error'],absolute_tolerance=1e-5,relative_tolerance=0,source_inputs_unchanged=True,previous_mismatch_list_byte_values_identical=identical,failed_frames=failed,input_pins=pins,complete_conversion_increment=0,implicit_sampling_verified=False,forge_render_verified=False,studio_verified=False,full_import=False,next_steps=['Compare raw albedo, parallax UV and coarse/fine derivatives in v625/v626','Correct the source-faithful conversion and rerun uninstrumented surface comparison','Qualify Forge depth/host, all 24 prefabs and Studio'])
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
header='''## House — comparaison des mipmaps implicites : écart conservé (v627)

- Six matériaux / 24 prefabs ; comparaison de 48 images et 288 sorties GPU.
- 284 sorties passent ; quatre sorties de couleur dépassent la tolérance absolue 0,00001 : 3 206 composantes, écart maximal 0,000703335.
- Orientation des pixels exacte ; normales, paramètres de surface, émission et bent normals passent dans ce banc.
- La macro HDRP exacte à biais zéro ne corrige pas cet écart : la liste des différences est identique au premier essai. Sources et importeurs privés inchangés.
- Diagnostic v625/v626 : couleur brute, UV parallax et dérivées fines/grossières. Ce diagnostic ne qualifie pas le matériau.
- Rendu Forge, profondeur, prefabs/colliders et disponibilité Studio restent à vérifier. Incrément de conversions complètes : **0**.

'''
(O/'LOT.md').write_text(header,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_text(header+before.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:result[k] for k in ['status','passed_planes','failed_planes','failed_components','complete_conversion_increment']}))
