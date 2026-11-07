"""Prepare reusable source materials/BC storage; keep host qualification pending."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,shutil
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'house-source-material-package-v647';G=W/'house-resolved-native-bc-comparison-v646';C=W/'house-resolved-sampling-checkpoint-v645'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def put(name,obj):
    p=O/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(obj,indent=2),'utf-8');outputs[str(p)]=sha(p);return str(p)
def copy(p,name):
    p=Path(p);h=sha(p);pins[str(p)]=h;t=O/name;t.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,t);assert sha(t)==h;outputs[str(t)]=h;return str(t)
assert not O.exists();gpu=read(G/'RESULT.v1.json');checkpoint=read(C/'RESULT.v1.json');assert gpu['failed_components']==0 and gpu['actual_gpu_exit_code']==0 and gpu['native_bc_sampling_with_quality_verified']
pins={};outputs={}
for key in ['input_pins','output_pins']:
    for p,h in gpu[key].items():assert sha(p)==h,p;pins[p]=h
lot=read(W/'house-main-material-lot-v603/RESULT.v1.json');assert len(lot['card_ids'])==24
top=read(W/'house-native-bound-surface-candidates-v615/RESULT.v1.json');assert len(top['rows'])==6
queue=read(W/'queue/house-on-a-hill/prefabs.json');source=read(queue['source_manifest']);archive=V/'originals/unity-purchased/hivemind/house-on-a-hill/House On A Hill Environment.unitypackage'
assert sha(archive)==queue['archive_sha256']==source['archive_sha256'];pins[str(archive)]=queue['archive_sha256']
O.mkdir();copy(queue['source_manifest'],'provenance/source-manifest.original.v1.json');license_path=copy(V/'licenses/hivemind/PROVENANCE.md','provenance/PROVENANCE.original.md')
policies={p['texture_guid']:p for p in read(C/'RESOLVED-SAMPLING-POLICIES.v1.json')};textures=[]
for t in read(C/'NATIVE-BC-MIP-CHAINS.v1.json'):
    target=copy(t['whole_path'],'textures/'+t['texture_guid']+'.native.bin');offset=0;levels=[]
    for l in t['levels']:levels.append(dict(level=l['level'],width=l['width'],height=l['height'],offset=offset,bytes=l['bytes'],sha256=l['sha256']));offset+=l['bytes']
    assert offset==Path(target).stat().st_size
    textures.append(dict(texture_guid=t['texture_guid'],path=target,sha256=t['whole_sha256'],native_format=t['native_format'],native_graphics_format=t['native_graphics_format'],levels=levels,sampling=policies[t['texture_guid']]))
materials=[]
for m in top['rows']:
    assert sha(m['wgsl'])==m['wgsl_sha256'];program=Path(m['wgsl']).read_text('utf-8-sig');assert 'fn evaluate_original_source_material' in program and '@fragment' not in program
    target=copy(m['wgsl'],'materials/'+m['material_guid']+'/source_material.wgsl');binding=read(m['native_binding_manifest']);bp=copy(m['native_binding_manifest'],'materials/'+m['material_guid']+'/NATIVE-BINDINGS.v1.json')
    materials.append(dict(material_guid=m['material_guid'],program=target,program_sha256=m['wgsl_sha256'],native_binding_manifest=bp,required_inputs=binding['required_inputs'],source_texture_bindings=m['source_texture_bindings']))
for p in [Path(__file__),G/'RESULT.v1.json',C/'RESULT.v1.json',W/'house-main-material-lot-v603/RESULT.v1.json',W/'house-native-bound-surface-candidates-v615/RESULT.v1.json',W/'queue/house-on-a-hill/prefabs.json']:pins[str(p)]=sha(p)
manifest=dict(schema='openrpg.source-material-package',schema_version=1,pack_id=queue['pack_id'],license=source['license'],private_only=True,ai_content_generation_used=False,archive_path=str(archive),archive_sha256=queue['archive_sha256'],license_provenance=license_path,source_shadergraph_guid='7dcd0e3bb8cbf2345a1e124f5a095198',source_card_ids=lot['card_ids'],materials=materials,textures=textures,sampling_quality='ForceEnable',global_mip_bias_fixture=0,qualification=dict(controlled_plane_surface_verified=True,implicit_sampling_verified=True,native_bc_sampling_verified=True,absolute_tolerance=1e-5,max_absolute_error=gpu['max_absolute_error'],forge_host_installed=False,prefab_render_verified=False,studio_verified=False,full_import=False))
put('MATERIAL-PACKAGE.v1.json',manifest)
result=dict(status='house_six_materials_native_bc_package_prepared',utc=datetime.now(timezone.utc).isoformat(),lot='L03.HOUSE.MAIN.01',materials=6,textures=18,mip_levels=216,prefab_cards=24,native_bc_bytes=sum(Path(t['path']).stat().st_size for t in textures),input_pins=pins,output_pins=outputs,source_inputs_unchanged=True,forge_host_installed=False,full_import=False,complete_conversion_increment=0)
put('RESULT.v1.json',result)
header='''## House — paquet de six matériaux et textures BC natives prêt (v647)

- Comparaison source/WGSL avec textures BC et qualité ForceEnable : 48 rendus, 288 sorties, **zéro dépassement** de 0,00001 ; écart maximal 0,000000059605.
- Six programmes WGSL sans banc de test, 18 ressources BC natives / 216 niveaux, bindings et politique de sampling regroupés dans F:/OpenRPG_ImportWork/house-source-material-package-v647/MATERIAL-PACKAGE.v1.json.
- Archive d'origine sur F et licence vérifiées ; provenance et IDs des 24 fiches conservés. Blocs BC du paquet : 83 886 480 octets.
- Prochaine tranche : raccordement aux matériaux communs Forge, profondeur POM, rendu des 24 prefabs et colliders, puis recherche/images et placement dans Studio.
- Paquet candidat privé : aucune promotion des fiches ni conversion complète annoncée. **Incrément complet : 0**.

'''
(O/'LOT.md').write_text(header,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_text(header+before.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:result[k] for k in ['status','materials','textures','mip_levels','native_bc_bytes','complete_conversion_increment']}))
