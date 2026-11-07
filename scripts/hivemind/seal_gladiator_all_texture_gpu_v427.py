"""Seal the measured twelve-texture resource lane, leaving material rendering open."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-all-texture-gpu-delivery-v427';S=Path(__file__).resolve().parent
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
normal=W/'gladiator-normal-gpu-delivery-v422/RESULT.v1.json';colors=W/'gladiator-color-opacity-comparison-v426/RESULT.v1.json'
n=read(normal);c=read(colors)
assert n['normal_texture_count']==4 and n['mips']==52 and n['measured_uv_mapping']=='identity'
assert c['texture_count']==8 and c['mips']==104 and c['identity_mapping_passed'] and c['common_measured_coordinate_mappings']==['identity']
for j in [n,c]:
    for p,h in j['input_pins'].items():assert sha(p)==h,p
alpha_rows=[r for r in c['rows'] if r['gpu_format']=='Rgba8Unorm'];assert len(alpha_rows)==2
assert all(r['samples_below_alpha_half']>0 and r['samples_at_or_above_alpha_half']>0 for r in alpha_rows)
maxima=[max(r['channel_max_errors']['identity'][i] for r in c['rows']) for i in range(4)]
inputs=[normal,colors,Path(__file__),S/'run_gladiator_normal_gpu_v417.py',S/'run_gladiator_color_opacity_gpu_v424.py',S/'run_gladiator_normal_reference_v415.py',S/'run_gladiator_color_opacity_reference_v423.py',S/'compare_gladiator_color_opacity_gpu_v426.py']
result=dict(status='all_twelve_texture_resources_gpu_compared_to_unity',updated_utc=datetime.now(timezone.utc).isoformat(),input_pins={str(p):sha(p) for p in inputs},textures=12,mips=156,sample_coordinates=638976,measured_uv_mapping='identity',color_opacity_channel_max_errors=maxima,opacity_alpha_textures=alpha_rows,normal_decoder_qualified_for_measured_samples=True,original_texture_resources_preserved=True,vault_modified=False,all_texture_pixels_sampled=False,full_material_render_qualified=False,complete_asset_conversion_increment=0,full_import=False,next_work='Measure native color-property transfer and native render state; compose full material stages and GLBs.')
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note='''## Gladiator — les 12 textures comparées sur GPU, 10 octobre 2026

12 textures, 156 niveaux, 638 976 coordonnées comparées entre Unity et le banc utilisant les adaptateurs Forge actuels. Orientation UV mesurée : identité. Les 8 textures couleur/ORM/opacité passent à 1e-6 par canal ; les 4 textures de normales et leur décodeur ont leur preuve séparée.

Les deux textures RGBA d'opacité présentent des échantillons sous et au-dessus de 0,5 ; leur alpha est conservé. RGB24 est transporté par l'adaptateur produit vers RGBA8 avec alpha opaque, sans modifier les sources ou les mipmaps natifs.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-all-texture-gpu-delivery-v427\\RESULT.v1.json
Restent transfert des propriétés couleur, état et rendu des matériaux, GLB et validation dans l'éditeur. Tous les pixels n'ont pas été testés ; aucun nouvel asset complet qualifié.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;b=p.read_bytes();(O/(name+'.before')).write_bytes(b);p.write_bytes(note.encode('utf-8')+b)
print(json.dumps(dict(status=result['status'],textures=12,mips=156,sample_coordinates=638976,color_opacity_channel_max_errors=maxima)))
