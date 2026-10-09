"""Reopen native geometry independently; no inferred tangents or material slots."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');CAP=W/'house-native-mesh-streams-v1';ROOT=W/'house-native-mesh-streams-reopen-v1'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def main():
    assert not ROOT.exists();ROOT.mkdir();state=dict(status='house_mesh_stream_reopen_refusal_retained',meshes_verified=0,geometry_converted=False,material_qualified=False,full_import=False)
    try:
        run=read(CAP/'RUN-RESULT.v1.json');assert run['status']=='48_house_native_mesh_streams_captured_reopen_pending'
        pins=read(CAP/'PREPARED.v1.json')['input_pins']
        for p,d in pins.items():assert sha(p)==d,p
        wanted={(m['guid'],m['file_id']):m for m in read(CAP/'REQUEST.v1.json')['meshes']}
        native=read(CAP/'NATIVE-RESULT.v1.json');assert len(native['meshes'])==48 and len(wanted)==48
        seen=set();verified=[]
        for row in native['meshes']:
            key=(row['mesh_guid'],row['mesh_file_id']);assert key in wanted and key not in seen;seen.add(key);expected=wanted[key]
            assert row['vertices']==expected['vertices'] and row['mesh_path']==expected['asset_path']
            directory=Path(row['directory']);assert directory.resolve().parent==CAP.resolve() and directory.name==key[0]+'-'+key[1]
            n=row['vertices'];assert n>0 and row['submeshes']>0
            attributes={s.split(':')[0] for s in expected['original_vertex_formats']}
            streams={'positions.f32':('Position',3),'normals.f32':('Normal',3),'tangents.f32':('Tangent',4),'colors.f32':('Color',4)}
            streams.update({f'uv{i}.f32':(f'TexCoord{i}',4) for i in range(8)})
            names=set(streams)|{f'submesh-{i}.i32' for i in range(row['submeshes'])};assert set(row['streams'])==names and len(row['streams'])==len(names)
            for name,(attribute,dimension) in streams.items():
                p=directory/name;data=p.read_bytes();assert len(data)==(n*dimension*4 if attribute in attributes else 0),(key,name)
                values=[v[0] for v in struct.iter_unpack('<f',data)];assert all(math.isfinite(v) for v in values),(key,name)
                if name=='positions.f32':bounds=[[min(values[a::3]),max(values[a::3])] for a in range(3)]
                pins[str(p)]=sha(p)
            triangles=0
            for i in range(row['submeshes']):
                p=directory/f'submesh-{i}.i32';data=p.read_bytes();assert len(data)%12==0
                indices=[v[0] for v in struct.iter_unpack('<i',data)];assert all(0<=index<n for index in indices),(key,i)
                triangles+=len(indices)//3;pins[str(p)]=sha(p)
            verified.append(dict(mesh_guid=key[0],mesh_file_id=key[1],vertices=n,submeshes=row['submeshes'],triangles=triangles,source_bounds_xyz=bounds,source_attributes=sorted(attributes),streams_reopened=len(names)))
        assert seen==set(wanted)
        for p in [Path(__file__).resolve(),CAP/'REQUEST.v1.json',CAP/'NATIVE-RESULT.v1.json',CAP/'RUN-RESULT.v1.json']:pins[str(p)]=sha(p)
        state.update(status='48_house_native_render_mesh_streams_independently_reopened',meshes_verified=48,vertices=sum(r['vertices'] for r in verified),triangles=sum(r['triangles'] for r in verified),rows=verified,input_pins=pins,source_inputs_unchanged=True,missing_attributes_not_generated=True)
    except Exception as exc:state['error']=repr(exc)
    state['finished_utc']=datetime.now(timezone.utc).isoformat();(ROOT/'RESULT.v1.json').write_text(json.dumps(state,indent=2),'utf-8');print(json.dumps({k:v for k,v in state.items() if k not in ['input_pins','rows']}))
if __name__=='__main__':main()
