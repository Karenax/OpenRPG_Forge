"""Convert native paired BakeMesh observations to reversible Forge GPU buffers.

Sampled geometry for render qualification only: never an autonomous particle system.
The native true mode already incorporates rotation/scale. Retain translation on
the instance; do not apply the full renderer matrix to these vertices again.
Only observed Local/Local modes are admitted. Preserve atlas UVs and Color32 bytes.
"""
from pathlib import Path
import argparse, hashlib, json, math, struct

def sha(raw):return hashlib.sha256(raw).hexdigest()
def floats(values,fields):
    assert all(set(v)==set(fields) for v in values),'Attribute schema'
    assert all(math.isfinite(v[k]) for v in values for k in fields),'Nonfinite source attribute'
    return b''.join(struct.pack('<f',v[k]) for v in values for k in fields)
def flip(raw,width,components):
    assert len(raw)%(width*4)==0
    result=bytearray(raw)
    for at in range(0,len(raw),width*4):
        for c in components:result[at+c*4+3]^=128
    return bytes(result)
def winding(raw):
    assert len(raw)%12==0
    return b''.join(raw[i:i+4]+raw[i+8:i+12]+raw[i+4:i+8] for i in range(0,len(raw),12))
def convert(case,row,g):
    assert row['native_simulation_space']=='Local' and row['native_scaling_mode']=='Local','Unmeasured simulation/scaling mode'
    assert g['render_mode']=='Billboard' and g['alignment']=='View','Unmeasured billboard mode'
    n=g['vertices'];assert n==row['count']*4 and n>0
    assert len(g['with_transform_positions'])==len(g['uv0'])==len(g['colors'])==n
    assert g['with_transform_colors']==g['colors'] and g['with_transform_indices']==g['indices']
    assert g['renderer_local_to_world']==g['particle_local_to_world']==row['local_to_world']
    streams={};source={}
    for name,field,width,axes in [('positions','xyz',3,[0]),('normals','xyz',3,[0]),('tangents','xyzw',4,[0,3])]:
        values=g['with_transform_'+name];assert len(values) in (0,n)
        raw=floats(values,field);target=flip(raw,width,axes);assert flip(target,width,axes)==raw
        streams[name+'.f32']=target;source[name+'.f32']=raw
    for name in ['uv0','uv1']:
        assert len(g[name]) in (0,n);raw=floats(g[name],'xyzw');streams[name+'.f32']=raw;source[name+'.f32']=raw
    assert all(set(v)==set('rgba') and all(type(v[k]) is int and 0<=v[k]<=255 for k in 'rgba') for v in g['colors'])
    raw=bytes(v[k] for v in g['colors'] for k in 'rgba');streams['colors.rgba8']=raw;source['colors.rgba8']=raw
    assert len(g['indices'])==row['count']*6 and all(type(v) is int and 0<=v<n for v in g['indices'])
    raw=struct.pack('<'+'I'*len(g['indices']),*g['indices']);target=winding(raw);assert winding(target)==raw
    streams['indices.u32']=target;source['indices.u32']=raw
    m=g['renderer_local_to_world'];assert set(m)=={f'e{i}{j}' for i in range(4) for j in range(4)} and all(math.isfinite(v) for v in m.values())
    translation=[-m['e03'],m['e13'],m['e23']]
    instance=[1,0,0,0,0,1,0,0,0,0,1,0,*translation,1]
    metadata=dict(schema_version=1,node_path=case['node_path'],prefab_guid=case['prefab_guid'],renderer_file_id=case['renderer_file_id'],particle_file_id=case['particle_file_id'],material_guid=case['material_guid'],seed=row['seed'],requested_time=row['requested_time'],native_system_time=row['native_system_time'],repetition=row['repetition'],camera_fixture=g['fixture'],vertices=n,triangles=len(g['indices'])//3,
        source_renderer_local_to_world=m,target_instance_matrix_column_major=instance,
        source_camera_local_to_world=g['camera_local_to_world'],source_world_to_camera=g['world_to_camera'],source_projection=g['projection'],
        source_sampling='native BakeMesh with transform=true; camera-specific geometry',source_basis_conversion='X reflection; tangent handedness and triangle winding reversed',rotation_scale_already_baked=True,translation_retained_on_instance=True,
        native_color_bytes_preserved=True,native_atlas_uvs_preserved=True,source_world_translation_raster_verified=False,autonomous_simulation=False,runtime_converted=False,full_import=False)
    return metadata,streams,source

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--input',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    assert args.input.is_file() and not args.output.exists()
    raw=args.input.read_bytes();report=json.loads(raw);plans=[]
    for case in report['cases']:
        for row in case['rows']:
            for g in row['geometry']:plans.append(convert(case,row,g))
    assert plans
    args.output.mkdir(parents=True)
    rows=[]
    for i,(meta,streams,original) in enumerate(plans):
        folder=args.output/f'{i:04d}';folder.mkdir();meta['streams']={}
        for name,data in streams.items():
            p=folder/name;p.write_bytes(data);assert p.read_bytes()==data
            meta['streams'][name]=dict(path=str(p),bytes=len(data),sha256=sha(data),source_bytes_sha256=sha(original[name]),source_bytes=len(original[name]))
        (folder/'FRAME.v1.json').write_text(json.dumps(meta,indent=2),'utf-8')
        rows.append(dict(frame=str(folder/'FRAME.v1.json'),sha256=sha((folder/'FRAME.v1.json').read_bytes()),vertices=meta['vertices'],triangles=meta['triangles']))
    assert args.input.read_bytes()==raw
    receipt=dict(status='native_baked_particle_observations_converted_to_reversible_forge_gpu_buffers',source=str(args.input),source_sha256=sha(raw),converter=str(Path(__file__)),converter_sha256=sha(Path(__file__).read_bytes()),frames=len(rows),vertices=sum(r['vertices'] for r in rows),triangles=sum(r['triangles'] for r in rows),rows=rows,native_snapshots_only=True,autonomous_simulation=False,source_world_translation_raster_verified=False,gpu_rendered=False,studio_verified=False,full_import=False)
    (args.output/'RESULT.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in receipt.items() if k!='rows'}))

if __name__=='__main__':main()
