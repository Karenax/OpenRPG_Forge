"""Keep successful hash diagnostics distinct from one remaining vertex mismatch."""
from pathlib import Path
import json,hashlib,struct,datetime
W=Path('F:/OpenRPG_ImportWork');S=Path(__file__).parent;O=W/'pirate-palm-noise-progress-v557'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
paths=[W/'pirate-palm-wind-gpu-comparison-v552/RESULT.v1.json',W/'pirate-palm-wind-gpu-comparison-v556/RESULT.v1.json']
noise,vertex=map(read,paths)
for receipt in [noise,vertex]:
    for p,h in receipt['pins'].items():assert sha(p)==h,p
assert noise['status']=='gpu_numeric_comparison_pass' and noise['failed_components']==0
assert noise['category_max']['hash_stage0']==noise['category_max']['hash_stage1']==0
assert vertex['failed_components']==1 and vertex['category_max']['wind_delta']<1e-5
failpath=W/'pirate-palm-wind-gpu-comparison-v556/MISMATCHES.v1.json';failures=read(failpath)
assert len(failures)==1 and failures[0]['field']=='position_x'
sample=failures[0]['sample'];inputpath=W/'pirate-palm-wind-native-reference-v541/INPUT.f32'
sample_input=struct.unpack_from('<24f',inputpath.read_bytes(),sample*96)
pins={str(p):sha(p) for p in paths+[failpath,inputpath,Path(__file__)]}
for name in ['run_palm_noise_intermediates_v547.py','run_palm_noise_wgsl_intermediates_v548.py','probe_palm_legacy_remainder_v549.py','probe_palm_reciprocal_remainder_v550.py','inspect_palm_fxc_assembly_v551.py','probe_palm_fxc_mad_v552.py','verify_palm_corrected_vertex_v553.py','inspect_palm_vertex_fxc_v554.py','verify_palm_fused_vertex_v555.py','verify_palm_centered_noise_v556.py']:
    pins[str(S/name)]=sha(S/name)
for version in [547,548,549,550,551,552,553,554,555,556]:
    prefix='pirate-palm-wind-native-reference' if version==547 else 'pirate-palm-fxc-assembly' if version in [551,554] else 'pirate-palm-wind-gpu-comparison'
    p=W/f'{prefix}-v{version}'/('RUN-RESULT.v1.json' if version==547 else 'RESULT.v1.json');pins[str(p)]=sha(p)
O.mkdir()
result=dict(status='noise_hash_gpu_parity_pass_one_vertex_rounding_mismatch_retained',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),samples=3192,
hash_stages_exact=True,noise_max_error=noise['category_max']['noise'],wind_max_error=vertex['category_max']['wind_delta'],remaining_failures=failures,remaining_sample_input=list(sample_input),
comparison_tolerance_unchanged=1e-5,new_admitted_assets=0,full_import=False,studio_verified=False,production_shader_changed=False,
pins=pins,next='Diagnose sample537 rounding: measure all four gradient directions, raw centered interpolant and normalization/dot/FMA operations on the native and WGSL backends; then rerun complete vertex without relaxing tolerance.')
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''# Reprise — diagnostic vent palmiers v547 à v557

Captures natives Unity et sondes WGSL GPU : 3 192 échantillons, sources intactes.
Cause principale mesurée : calcul du reste et opérations fusionnées du hachage. La sonde corrigée v552 passe avec les deux étapes de hash exactement identiques.
Vertex complet v556 : 1 composante hors tolérance absolue 1e-5 sur 38 304, contre 4 769 dans v545.
Vent : erreur maximale 0,0000004732 ; normales/tangentes identiques ; position X de l'échantillon 537 : écart 0,0000305176.
La tolérance n'est pas relevée. Prochaine étape : arrondi du gradient/interpolant puis nouveau contrôle vertex complet.
Preuve : F:\\OpenRPG_ImportWork\\pirate-palm-noise-progress-v557\\RESULT.v1.json.
Les modifications restent dans les bancs isolés sur F ; aucun shader produit ni catalogue modifié. Six variantes palmiers toujours non admises.

'''
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;old=p.read_bytes();(O/(name+'.before')).write_bytes(old);p.write_text(note+old.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['pins','remaining_sample_input']}))
