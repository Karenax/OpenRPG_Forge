"""Seal complete native House mip chains; do not claim an implicit-LOD render pass."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');N=W/'house-native-texture-mips-v619';O=W/'house-native-texture-mips-checkpoint-v620'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
run=read(N/'RUN-RESULT.v1.json');assert run['status']=='house_native_mips_captured_review_pending' and run['unity_exit_code']==0 and not run['source_changes'] and not run['private_importer_changes']
prep=read(N/'PREPARED.v1.json');native=read(N/'NATIVE-RESULT.v1.json');pins=dict(prep['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
assert native['source_texture_settings_unchanged'] and len(native['textures'])==18 and sum(len(t['levels']) for t in native['textures'])==216
outputs={};rows=[];new=0;reused=0;bytes_total=0
for texture in native['textures']:
    levels=texture['levels'];assert texture['texture_settings_unchanged'] and len(levels)==texture['native_mips']
    assert [m['level'] for m in levels]==list(range(texture['native_mips']))
    width,height=levels[0]['width'],levels[0]['height'];assert levels[-1]['width']==levels[-1]['height']==1
    manifest=[]
    for m in levels:
        assert m['width']==max(1,width>>m['level']) and m['height']==max(1,height>>m['level'])
        assert Path(m['path']).stat().st_size==m['bytes']==m['width']*m['height']*16
        assert m['reused_existing_proof']==(m['level']==0)
        h=sha(m['path']);outputs[m['path']]=h;bytes_total+=m['bytes'];new+=not m['reused_existing_proof'];reused+=m['reused_existing_proof']
        if m['level']==0:assert pins[m['path']]==h
        manifest.append(dict(level=m['level'],width=m['width'],height=m['height'],path=m['path'],sha256=h,bytes=m['bytes'],reused_existing_proof=m['reused_existing_proof']))
    rows.append(dict(texture_guid=texture['guid'],native_format=texture['format'],native_graphics_format=texture['graphics_format'],transport_format='GPU-decoded RGBA f32; do not apply sRGB conversion again',levels=manifest))
assert new==198 and reused==18
for p in [Path(__file__),N/'RUN-RESULT.v1.json',N/'NATIVE-RESULT.v1.json',N/'PREPARED.v1.json']:pins[str(p)]=sha(p)
O.mkdir();mp=O/'MIP-CHAINS.v1.json';mp.write_text(json.dumps(rows,indent=2),'utf-8');outputs[str(mp)]=sha(mp)
result=dict(status='house_eighteen_native_mip_chains_integrity_verified',utc=datetime.now(timezone.utc).isoformat(),lot='L03.HOUSE.MAIN.01',materials=6,prefab_cards=24,textures=18,mip_levels=216,new_mip_captures=198,mip_zero_reused=18,total_decoded_bytes=bytes_total,source_inputs_unchanged=True,native_settings_unchanged=True,input_pins=pins,output_pins=outputs,implicit_derivative_render_verified=False,forge_depth_host_verified=False,studio_verified=False,full_import=False,complete_conversion_increment=0,next_steps=['Use complete native mip chains in the WGSL reference renderer','Compare actual generated surface rasterization with implicit sample derivatives','Qualify Forge depth/host and all 24 prefabs/colliders','Reopen the assets in actual Studio'])
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
header='''## House — mipmaps natives intégralement conservées (v620)

- Six matériaux / 24 prefabs : 18 textures, 216 niveaux de mipmaps GPU vérifiés par dimensions, taille et SHA-256.
- 198 nouveaux niveaux capturés ; les 18 niveaux zéro déjà vérifiés ont été réutilisés sans recalcul.
- Texture et importeur source inchangés. Transport RGBA float déjà décodé par Unity : aucune nouvelle conversion sRGB à appliquer.
- Comparaison de surface à mip zéro v618 conservée. Rendu avec dérivées/LOD implicites encore à comparer, puis profondeur moteur, prefabs/colliders et Studio.
- Incrément de conversions complètes : **0**.

'''
(O/'LOT.md').write_text(header,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_text(header+before.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:result[k] for k in ['status','textures','mip_levels','new_mip_captures','mip_zero_reused','complete_conversion_increment']}))
