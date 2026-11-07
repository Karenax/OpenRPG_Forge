"""Publish the current lot checkpoint without promoting it to render qualification."""
from pathlib import Path
import hashlib,json
from datetime import datetime,timezone
W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-kernel-normal-checkpoint-v416'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
k=W/'gladiator-material-kernels-v414/RESULT.v1.json';r=W/'gladiator-normal-reference-v415/GPU-REFERENCE-REOPEN.v1.json';run=W/'gladiator-normal-reference-v415/RUN-RESULT.v1.json'
kernels=read(k);reference=read(r);execution=read(run)
assert len(kernels['rows'])==6 and kernels['failures']==[]
for p,h in kernels['input_pins'].items():assert sha(Path(p))==h
for row in kernels['rows']:assert sha(Path(row['wgsl']))==row['wgsl_sha256'] and row['actual_naga_exit_code']==0
assert reference['verified_textures']==4 and reference['sampled_texel_lod_coordinates']==212992
assert execution['unity_exit_code']==0 and not execution['source_changes'] and not execution['private_importer_changes']
for row in reference['rows']:
    for p,h in row['raw_rgba_and_both_decode_buffers_sha256'].items():assert sha(Path(p))==h
O.mkdir()
result=dict(status='six_kernels_validated_and_four_source_normal_references_reopened',updated_utc=datetime.now(timezone.utc).isoformat(),input_pins={str(p):sha(p) for p in [k,r,run,Path(__file__)]},material_kernels=6,source_normal_textures=4,source_normal_mips=52,source_normal_sample_coordinates=212992,source_inputs_unchanged=True,forge_gpu_sampling_parity_qualified=False,material_render_parity_qualified=False,complete_asset_conversion_increment=0,full_import=False,next_work='Compare Forge sampling and decoder with the measured Unity buffers; construct native stages and GLB only with explicit source render state.')
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — programmes matériaux et références des normales, 10 octobre 2026

6 programmes WGSL générés à partir des graphes source et validés par Naga. Le canal alpha d'opacité séparé est conservé dans le programme des drapeaux.
4 textures de normales : 52 niveaux mesurés dans Unity, 212 992 coordonnées de texels réouvertes et vérifiées. Les sources et les copies privées sont inchangées pendant cette mesure.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-kernel-normal-checkpoint-v416\\RESULT.v1.json
Ces références Unity ne prouvent pas encore le décodage ou le rendu Forge. Restent comparaison GPU Forge/Unity, stages de rendu, GLB et vérification dans l'éditeur. Aucun nouvel asset complet qualifié.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(result['status'])
