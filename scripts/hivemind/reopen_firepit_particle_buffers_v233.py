"""Compare every converted particle stream independently with the native capture."""
from pathlib import Path
import copy,datetime,hashlib,json,struct
import numpy as np
import convert_unity_baked_particle_frames as converter
W=Path('F:/OpenRPG_ImportWork');I=W/'firepit-particle-forge-buffers-v232';O=W/'firepit-particle-forge-buffers-reopen-v233'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
receipt=read(I/'RESULT.v1.json');source=Path(receipt['source']);assert sha(source)==receipt['source_sha256']
native=read(source);run=read(source.parent/'RUN-RESULT.v1.json');upstream=source.parent/'INDEPENDENT-REOPEN.v1.json'
assert run['unity_exit_code']==0 and run['native_geometry_verified'] and sha(upstream)==run['independent_receipt_sha256']
assert read(upstream)['capture_sha256']==sha(source)
assert sha(Path(receipt['converter']))==receipt['converter_sha256']
expected=[(case,row,g) for case in native['cases'] for row in case['rows'] for g in row['geometry']]
assert len(expected)==receipt['frames']==len(receipt['rows'])==336
pins={str(p):sha(p) for p in [Path(__file__),Path(converter.__file__),I/'RESULT.v1.json',source,upstream,source.parent/'RUN-RESULT.v1.json']}
stream_count=bytes_checked=0;max_center_error=0.;world_bounds={}
for entry,(case,row,g) in zip(receipt['rows'],expected):
    p=Path(entry['frame']);assert p.is_relative_to(I) and sha(p)==entry['sha256'];meta=read(p);pins[str(p)]=sha(p)
    assert (meta['node_path'],meta['seed'],meta['requested_time'],meta['repetition'],meta['camera_fixture'])==(case['node_path'],row['seed'],row['requested_time'],row['repetition'],g['fixture'])
    assert meta['material_guid']==case['material_guid'] and meta['renderer_file_id']==case['renderer_file_id']
    assert meta['source_renderer_local_to_world']==g['renderer_local_to_world']
    m=np.array([[g['renderer_local_to_world'][f'e{i}{j}'] for j in range(4)] for i in range(4)])
    translation=np.array([-m[0,3],m[1,3],m[2,3]])
    assert meta['target_instance_matrix_column_major']==[1,0,0,0,0,1,0,0,0,0,1,0,*translation.tolist(),1]
    for name,stream in meta['streams'].items():
        file=Path(stream['path']);assert file.parent==p.parent and sha(file)==stream['sha256']
        raw=file.read_bytes();assert len(raw)==stream['bytes'];pins[str(file)]=sha(file);bytes_checked+=len(raw);stream_count+=1
        if name=='indices.u32':
            values=np.frombuffer(raw,dtype='<u4').reshape(-1,3);original=values[:,[0,2,1]].copy().tobytes()
            assert original==np.asarray(g['indices'],dtype='<u4').tobytes()
        elif name=='colors.rgba8':
            original=raw;assert original==bytes(v[k] for v in g['with_transform_colors'] for k in 'rgba')
        else:
            key=name.split('.')[0];fields='xyz' if key in ['positions','normals'] else 'xyzw'
            values=g['with_transform_'+key] if key in ['positions','normals','tangents'] else g[key]
            original_bits=np.frombuffer(raw,dtype='<u4').reshape(-1,len(fields)).copy()
            if key in ['positions','normals','tangents']:original_bits[:,0]^=0x80000000
            if key=='tangents':original_bits[:,3]^=0x80000000
            original=original_bits.tobytes();reference=b''.join(struct.pack('<f',v[k]) for v in values for k in fields)
            assert original==reference,(meta['node_path'],name)
        assert hashlib.sha256(original).hexdigest()==stream['source_bytes_sha256']
    vertices=np.frombuffer(Path(meta['streams']['positions.f32']['path']).read_bytes(),dtype='<f4').reshape(-1,3).astype(np.float64)
    world=vertices+translation;unreflected=world*np.array([-1,1,1]);centers=unreflected.reshape(-1,4,3).mean(axis=1)
    particles=np.array([[v['position'][k] for k in 'xyz'] for v in row['particles']]);expected_centers=particles@m[:3,:3].T+m[:3,3]
    max_center_error=max(max_center_error,float(np.max(np.abs(centers-expected_centers))))
    box=world_bounds.setdefault(case['node_path'],dict(min=[float('inf')]*3,max=[float('-inf')]*3))
    box['min']=np.minimum(box['min'],world.min(axis=0)).tolist();box['max']=np.maximum(box['max'],world.max(axis=0)).tolist()
negative=[];case,row,g=expected[0]
for kind in ['simulation_space','scaling_mode','billboard_alignment','nonfinite_position','missing_uv','invalid_index']:
    c,r,b=copy.deepcopy((case,row,g))
    if kind=='simulation_space':r['native_simulation_space']='Custom'
    elif kind=='scaling_mode':r['native_scaling_mode']='Hierarchy'
    elif kind=='billboard_alignment':b['alignment']='World'
    elif kind=='nonfinite_position':b['with_transform_positions'][0]['x']=float('nan')
    elif kind=='missing_uv':b['uv0']=[]
    else:b['indices'][0]=b['vertices'];b['with_transform_indices'][0]=b['vertices']
    try:converter.convert(c,r,b)
    except AssertionError:negative.append(kind)
    else:raise AssertionError('Malformed or unmeasured source accepted: '+kind)
O.mkdir();result=dict(status='all_native_particle_streams_reopened_and_inverse_bits_verified',input_pins=pins,frames=336,streams=stream_count,bytes_reopened=bytes_checked,vertices=receipt['vertices'],triangles=receipt['triangles'],native_stream_inverse_byte_equal=True,negative_controls=negative,maximum_world_quad_center_vs_source_local_particle_transform_error=max_center_error,world_bounds=world_bounds,world_center_comparison_is_numeric_not_raster_proof=True,native_snapshots_only=True,autonomous_simulation=False,source_world_translation_raster_verified=False,gpu_rendered=False,studio_verified=False,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
block='''## Effets du foyer L06 — géométries natives converties en buffers Forge (v232/v233)

**336 géométries converties et réouvertes : 30 144 sommets, 15 072 triangles.** UV d'atlas et Color32 natifs préservés ; positions/normales/tangentes et winding inversés selon la base Forge, avec retour exact aux octets source. Translation gardée sur l'instance, rotation/échelle déjà fournies par BakeMesh(true). Six entrées malformées ou modes non mesurés refusés.

Ce sont des buffers des instantanés natifs pour la qualification du rendu. **Pas de simulation autonome ni d'effets complets qualifiés.** La comparaison numérique des centres avec les particules locales et leur matrice source est conservée dans le reçu ; elle ne constitue pas une preuve de rendu/raster. Prochaine étape : renderer d'effets avec bindings textures/samplers source, comparaison source/Forge et construction autonome des billboards/particules. Aucun changement du catalogue Vault ou des sources Unity.

[Conversion](../firepit-particle-forge-buffers-v232/RESULT.v1.json) · [Réouverture indépendante](../firepit-particle-forge-buffers-reopen-v233/RESULT.v1.json).

'''
for name in ['POINT_DE_REPRISE.md','AVANCEMENT_ACTUEL.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;old=p.read_text('utf-8-sig');(O/('BEFORE-'+name)).write_text(old,'utf-8');p.write_text(block+old,'utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
