"""Seal actual sampling/decoder parity without extending it to material rendering."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');S=Path(__file__).resolve().parent;O=W/'gladiator-normal-gpu-delivery-v422'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
assert not O.exists()
proof=W/'gladiator-normal-gpu-comparison-v421/RESULT.v1.json';j=read(proof)
assert j['status']=='four_normal_gpu_sampling_and_decoder_comparisons_passed'
assert j['normal_texture_raw_sampling_verified'] and j['candidate_mode2_matches_float_rgorag_gpu_reference'] and j['all_match_builtin_unpack_reference']
assert j['common_measured_coordinate_mappings']==['identity'] and j['normal_textures']==4 and j['mips']==52
for p,h in j['input_pins'].items():assert sha(p)==h
k=W/'gladiator-material-kernels-v419/RESULT.v1.json';kernels=read(k);assert len(kernels['rows'])==6 and not kernels['failures']
for p,h in kernels['input_pins'].items():assert sha(p)==h
for r in kernels['rows']:assert sha(r['wgsl'])==r['wgsl_sha256']
inputs=[proof,k,Path(__file__),S/'generate_gladiator_material_kernels_v414.py',S/'generate_gladiator_material_kernels_v419.py',S/'run_gladiator_normal_gpu_v417.py',S/'run_gladiator_normal_gpu_v420.py',S/'compare_gladiator_normal_gpu_v418.py',S/'compare_gladiator_normal_gpu_v421.py']
maxima={key:[max(r['mappings']['identity'][key][c] for r in j['rows']) for c in range(4)] for key in ['raw','decoded','builtin']}
result=dict(status='four_native_normals_match_current_forge_adapter_gpu_and_corrected_kernel_decoder',updated_utc=datetime.now(timezone.utc).isoformat(),input_pins={str(p):sha(p) for p in inputs},normal_texture_count=4,mips=52,sample_coordinates=212992,measured_uv_mapping='identity',channel_max_errors=maxima,tolerances=j['tolerances'],source_inputs_unchanged=True,corrected_material_kernels=6,full_material_render_qualified=False,all_texture_pixels_sampled=False,complete_asset_conversion_increment=0,vault_modified=False,full_import=False,next_work='Qualify base-color transfer, opacity and source material render state; compose native forward/prepass stages and complete GLB carriers.')
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — comparaison GPU des normales validée, 10 octobre 2026

4 textures de normales, 52 niveaux et 212 992 coordonnées comparées entre Unity D3D11 et le banc D3D12 utilisant les adaptateurs Forge actuels. Lecture brute identique ; décodeur RG/AG corrigé conforme aux références float et UnpackNormal de Unity dans les seuils conservés. Orientation UV mesurée : identité.

6 programmes matériaux régénérés avec ce décodeur et validés par Naga. Le premier refus de décodage reste conservé sous gladiator-normal-gpu-comparison-v418.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-normal-gpu-delivery-v422\\RESULT.v1.json
Restent transfert des couleurs, opacité et état de rendu source, stages de rendu, GLB et validation dans l'éditeur. La comparaison des normales ne qualifie pas le rendu complet. Aucun nouvel asset complet qualifié.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;b=p.read_bytes();(O/(name+'.before')).write_bytes(b);p.write_bytes(note.encode('utf-8')+b)
print(json.dumps(dict(status=result['status'],channel_max_errors=maxima)))
