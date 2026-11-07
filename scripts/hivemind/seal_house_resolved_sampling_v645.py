"""Seal measured source quality, successful raster parity and exact BC storage."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
from resolve_unity_sampling_policy_v267 import resolve_unity_sampling_policy
W=Path('F:/OpenRPG_ImportWork');G=W/'house-resolved-anisotropy-comparison-v643';Q=W/'house-native-sampling-policy-v644';BC=W/'house-compressed-storage-v641';O=W/'house-resolved-sampling-checkpoint-v645'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
gpu=read(G/'RESULT.v1.json');quality=read(Q/'NATIVE-RESULT.v1.json');storage=read(BC/'NATIVE-RESULT.v1.json');pins={}
assert gpu['status']=='house_implicit_surface_gpu_comparison_pass' and gpu['failed_components']==gpu['orientation_marker_failed_components']==0 and gpu['actual_gpu_exit_code']==0
assert gpu['materials']==6 and gpu['frames']==48 and gpu['planes']==288
assert read(G/'PREPARED.v1.json')['absolute_tolerance']==1e-5
assert quality['quality_anisotropy']=='ForceEnable' and quality['global_mipmap_limit']==0
for root in [Q,BC]:
    run=read(root/'RUN-RESULT.v1.json');assert run['unity_exit_code']==0 and not run['source_changes'] and not run['private_importer_changes']
    for p,h in read(root/'PREPARED.v1.json')['input_pins'].items():assert sha(p)==h,p;pins[p]=h
for key in ['input_pins','output_pins']:
    for p,h in gpu[key].items():assert sha(p)==h,p;pins[p]=h
assert len(quality['textures'])==len(storage['textures'])==18 and {r['guid'] for r in quality['textures']}=={r['guid'] for r in storage['textures']}
profiles=[];chains=[];native_bytes=0
for t in quality['textures']:
    assert t['source_texture_settings_unchanged'] and t['mip_bias']==0 and t['aniso']==1 and t['filter']=='Bilinear'
    p=resolve_unity_sampling_policy(t['aniso'],1,quality['quality_anisotropy'],quality['graphics_api']);assert p['effective_anisotropy']==9 and p['effective_mip_filter']=='Linear';profiles.append(dict(texture_guid=t['guid'],native_texture=t,resolved_policy=p))
for t in storage['textures']:
    assert t['native_decoded_mips_equal'] and t['restored_decoded_mips_equal'] and t['importer_restored_equal'] and t['meta_bytes_restored_equal']
    assert sha(t['whole_path'])==t['whole_sha256'];pins[t['whole_path']]=t['whole_sha256'];levels=[]
    assert [l['level'] for l in t['levels']]==list(range(12))
    concat=hashlib.sha256()
    for l in t['levels']:
        b=Path(l['path']).read_bytes();assert len(b)==l['bytes']==max(1,(l['width']+3)//4)*max(1,(l['height']+3)//4)*(8 if t['format']=='DXT1' else 16);h=hashlib.sha256(b).hexdigest();pins[l['path']]=h;native_bytes+=len(b);concat.update(b);levels.append(dict(**l,sha256=h))
    assert concat.hexdigest()==t['whole_sha256']
    chains.append(dict(texture_guid=t['guid'],native_format=t['format'],native_graphics_format=t['graphics_format'],whole_path=t['whole_path'],whole_sha256=t['whole_sha256'],levels=levels))
for p in [Path(__file__),G/'RESULT.v1.json',G/'PREPARED.v1.json',Q/'RUN-RESULT.v1.json',Q/'NATIVE-RESULT.v1.json',BC/'RUN-RESULT.v1.json',BC/'NATIVE-RESULT.v1.json',Path(__file__).with_name('resolve_unity_sampling_policy_v267.py')]:pins[str(p)]=sha(p)
O.mkdir();outputs={}
for name,data in [('NATIVE-BC-MIP-CHAINS.v1.json',chains),('RESOLVED-SAMPLING-POLICIES.v1.json',profiles)]:
    p=O/name;p.write_text(json.dumps(data,indent=2),'utf-8');outputs[str(p)]=sha(p)
result=dict(status='house_implicit_surface_sampling_with_native_quality_verified',utc=datetime.now(timezone.utc).isoformat(),lot='L03.HOUSE.MAIN.01',materials=6,prefab_cards=24,frames=48,planes=288,compared_floats=gpu['compared_floats'],failed_components=0,absolute_tolerance=1e-5,max_absolute_error=gpu['max_absolute_error'],native_quality_mode=quality['quality_anisotropy'],source_declared_anisotropy=1,effective_anisotropy=9,source_declared_filter='Bilinear',effective_mip_filter='Linear',nominal_source_settings_changed=False,source_inputs_unchanged=True,native_bc_textures=18,native_bc_mip_levels=216,native_bc_bytes=native_bytes,global_mip_bias_fixture=0,implicit_sampling_verified=True,native_bc_wgsl_raster_verified=False,forge_depth_host_verified=False,prefab_mesh_render_verified=False,studio_verified=False,full_import=False,complete_conversion_increment=0,input_pins=pins,output_pins=outputs,next_steps=['Render unchanged WGSL with retained BC resources and resolved native quality','Integrate source quality sampling policy into the common Forge material path','Qualify depth/host and all 24 source prefabs/colliders','Verify asset search, images and use in actual Studio'])
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
header=f'''## House — comparaison de surface implicite validée avec la qualité native (v645)

- Six matériaux / 24 prefabs : 48 rendus, 288 sorties, {gpu['compared_floats']:,} valeurs comparées ; **zéro dépassement** de la tolérance 0,00001. Écart maximal : {gpu['max_absolute_error']:.9g}.
- Cause corrigée dans le banc : politique Unity ForceEnable omise. L'API native confirme ForceEnable ; le résolveur existant impose anisotropie 9 et interpolation linéaire des mipmaps. Déclarations source Bilinear/aniso 1 et shader WGSL conservés.
- 18 textures BC / 216 mipmaps natives conservées par SHA-256 ; tous les texels décodés vérifiés avant lisibilité, après lisibilité et après restauration. Sources, métadonnées et importeurs privés inchangés.
- Comparaison sur plans contrôlés, biais global zéro. Les anciens échecs restent conservés. L'emploi des blocs BC dans WGSL, le raccordement au sampler commun Forge, la profondeur, les prefabs/colliders et Studio restent à qualifier.
- Incrément de conversions complètes : **0**.

'''
(O/'LOT.md').write_text(header,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_text(header+before.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:result[k] for k in ['status','frames','planes','failed_components','native_bc_bytes','complete_conversion_increment']}))
