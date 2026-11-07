"""Reopen every captured mip and prove resource/readability restoration separately."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');I=W/'gladiator-native-texture-storage-v412';O=W/'gladiator-native-texture-seal-v413'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def capture_file(name):
    p=Path(name).resolve();assert p.is_relative_to(I.resolve()) and p.is_file(),p
    outputs[str(p)]=sha(p);return p.read_bytes()
assert not O.exists()
run=read(I/'RUN-RESULT.v1.json');prep=read(I/'PREPARED.v1.json');native=read(I/'NATIVE-RESULT.v1.json')
assert run['unity_exit_code']==0 and run['source_inputs_unchanged'] and 'finished_utc' in run
assert run['source_changes']==[] and sha(I/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
for p,h in prep['input_pins'].items():assert sha(p)==h,p
assert native['graphics_api']=='Direct3D11' and len(native['rows'])==12
assert len({r['guid'] for r in native['rows']})==12
assert set(r['guid'] for r in native['rows'])==set(prep['expected_textures'])
outputs={};rows=[];mips=0;samples=0;storage=0
for r in native['rows']:
    expected=prep['expected_textures'][r['guid']]
    assert not r['error'] and r['settings_restored_equal'] and r['gpu_samples_bit_exact']
    assert r['settings_before']==r['settings_restored'] and json.loads(r['settings_before'])==json.loads(r['settings_restored'])
    for a,b in [('format','format'),('graphics_format','graphics_format'),('width','width'),('height','height'),('mip_count','mips'),('aniso','aniso_level')]:assert r[a]==expected[b]
    assert r['filter_mode']=={'Point':0,'Bilinear':1,'Trilinear':2}[expected['filter_mode']]
    assert r['width']==4096 and r['height']==4096 and r['mip_count']==13
    source=capture_file(r['source_samples']);readable=capture_file(r['readable_samples'])
    assert source==readable and len(source)==r['sample_count']*16
    assert all(math.isfinite(v[0]) for v in struct.iter_unpack('<f',source))
    whole=capture_file(r['whole_file']);assert len(whole)==r['whole_bytes']
    assert len(r['mips'])==r['mip_count'];offset=0
    for level,m in enumerate(r['mips']):
        assert m['level']==level
        w=max(1,r['width']>>level);h=max(1,r['height']>>level)
        assert m['width']==w and m['height']==h
        size=w*h*{'RGB24':3,'RGBA32':4}[r['format']] if r['format'] in {'RGB24','RGBA32'} else max(1,(w+3)//4)*max(1,(h+3)//4)*(8 if r['format']=='DXT1' else 16)
        data=capture_file(m['file']);assert len(data)==size==m['bytes'] and whole[offset:offset+size]==data
        offset+=size;mips+=1
    assert offset==len(whole);storage+=len(whole);samples+=r['sample_count']
    rows.append(dict(guid=r['guid'],format=r['format'],graphics_format=r['graphics_format'],width=r['width'],height=r['height'],mips=r['mip_count'],bytes=len(whole),sample_count=r['sample_count'],sampled_gpu_texels_bit_exact_before_after_readability=True,importer_settings_restored_exact=True,filter_mode=r['filter_mode'],wrap_u=r['wrap_u'],wrap_v=r['wrap_v'],wrap_w=r['wrap_w'],aniso=r['aniso'],mip_bias=r['mip_bias']))
changes=[]
for r in prep['private_copies']:
    assert sha(r['snapshot'])==r['private_before_sha256']
    now=sha(r['copy'])
    if now!=r['private_before_sha256']:
        assert r['copy'].endswith('.png.meta'),r['copy']
        changes.append(dict(path=r['copy'],before_sha256=r['private_before_sha256'],after_sha256=now,before_snapshot=r['snapshot'],byte_parity=False,semantic_importer_restoration_checked=True))
assert set(run['private_importer_changes'])==set(r['path'] for r in changes)
O.mkdir()
result=dict(status='twelve_native_texture_chains_independently_reopened',updated_utc=datetime.now(timezone.utc).isoformat(),rows=rows,texture_count=12,mip_count=mips,sampled_texel_count=samples,unique_storage_bytes=storage,output_pins=outputs,input_pins={str(p):sha(p) for p in [I/'PREPARED.v1.json',I/'RUN-RESULT.v1.json',I/'NATIVE-RESULT.v1.json',Path(__file__)]},private_metadata_changes=changes,source_inputs_unchanged=True,vault_modified=False,full_texture_sampling_fidelity_qualified=False,material_gpu_qualified=False,complete_asset_conversion_increment=0,full_import=False,next_work='Create the faithful alpha/normal/color material path using the pinned actual texture resources, then validate GLB and product rendering.')
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note=f'''## Gladiator — textures natives réouvertes, 10 octobre 2026

12 textures Unity vérifiées, {mips} niveaux de détail, {samples} texels GPU comparés avant/après activation temporaire de la lecture : égalité exacte. Chaque bloc de chaque niveau a été réouvert et comparé au stockage complet. Les formats réels DXT1, DXT5, RGB24 et RGBA32 sont conservés.

Sources inchangées ; réglages des importeurs privés restaurés exactement dans Unity. {len(changes)} fichiers .meta privés réécrits par Unity sont conservés avec snapshots avant ; cela ne vaut pas égalité des octets des copies privées. Le Vault n'a pas été modifié.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-native-texture-seal-v413\\RESULT.v1.json
Restent fidélité du rendu des matériaux, construction des GLB, validation et disponibilité dans l'éditeur. Aucun nouvel asset complet qualifié.

'''
(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps(dict(status=result['status'],textures=12,mips=mips,samples=samples,unique_storage_bytes=storage,private_meta_changes=len(changes))))
