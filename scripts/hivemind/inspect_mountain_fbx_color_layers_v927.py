"""Read binary FBX node structure independently of Unity, with source hashes."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-original-fbx-color-review-v927'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def parse_fbx(data):
    assert data[:23]==b'Kaydara FBX Binary  \x00\x1a\x00'
    version=struct.unpack_from('<I',data,23)[0];wide=version>=7500;header=25 if wide else 13
    fmt='<QQQB' if wide else '<IIIB';nodes=[]
    def parse_properties(start,end,count):
        strings=[];pos=start
        for _ in range(count):
            assert pos<end;t=chr(data[pos]);pos+=1
            if t in 'YCFDIL':pos+=dict(Y=2,C=1,F=4,D=8,I=4,L=8)[t]
            elif t in 'SR':
                assert pos+4<=end;n=struct.unpack_from('<I',data,pos)[0];pos+=4
                assert pos+n<=end
                if t=='S':strings.append(data[pos:pos+n].decode('utf-8'))
                pos+=n
            elif t in 'fdlibc':
                assert pos+12<=end;n,encoding,size=struct.unpack_from('<III',data,pos);pos+=12
                assert encoding in (0,1) and pos+size<=end
                if encoding==0:assert size==n*dict(f=4,d=8,l=8,i=4,b=1,c=1)[t]
                pos+=size
            else:raise ValueError('Unsupported FBX property type '+repr(t))
            assert pos<=end
        assert pos==end
        return strings
    def node(offset,limit,path,depth):
        assert depth<=64 and len(nodes)<100000 and offset+header<=limit
        end,count,length,namelen=struct.unpack_from(fmt,data,offset)
        if end==count==length==namelen==0:return None,offset+header
        assert offset+header+namelen+length<=end<=limit
        name=data[offset+header:offset+header+namelen].decode('ascii');props_start=offset+header+namelen;props_end=props_start+length
        strings=parse_properties(props_start,props_end,count);row=dict(name=name,path=path+[name],strings=strings,offset=offset,end=end);nodes.append(row)
        pos=props_end
        while pos<end:
            child,pos=node(pos,end,row['path'],depth+1)
            if child is None:assert pos==end;break
        assert pos==end
        return row,end
    pos=27
    while pos+header<=len(data):
        row,pos=node(pos,len(data),[],0)
        if row is None:break
    assert nodes and any(n['name']=='Objects' for n in nodes)
    geometries=[]
    for geometry in [n for n in nodes if n['name']=='Geometry' and 'Mesh' in n['strings']]:
        descendants=[n for n in nodes if geometry['offset']<n['offset']<geometry['end']]
        geometries.append(dict(names=geometry['strings'],vertex_color_layers=sum(n['name']=='LayerElementColor' for n in descendants),normal_layers=sum(n['name']=='LayerElementNormal' for n in descendants),uv_layers=sum(n['name']=='LayerElementUV' for n in descendants)))
    assert geometries
    return dict(version=version,nodes=len(nodes),mesh_geometries=geometries,total_vertex_color_layers=sum(n['name']=='LayerElementColor' for n in nodes))
def main():
    native_path=W/'mountain-native-vertex-diagnostic-v925/NATIVE-RESULT.v1.json';stage_path=W/'mountain-hdrp-capture-stage-v869/RESULT.v1.json'
    native,stage=read(native_path),read(stage_path);project=W/'mountain-hdrp-capture-stage-v869/project'
    copies={str(Path(c['target'])):c for c in stage['copies']}
    pins={str(p):sha(p) for p in [native_path,stage_path,Path(__file__).resolve()]};files={}
    for card in native['cards']:
        for renderer in card['renderers']:
            target=project/renderer['mesh_path'];entry=copies[str(target)];source=Path(entry['source'])
            assert sha(source)==sha(target)==entry['sha256']
            key=str(source)
            if key not in files:files[key]=dict(source=key,target=str(target),sha256=sha(source),structure=parse_fbx(source.read_bytes()),card_ids=[])
            if card['id'] not in files[key]['card_ids']:files[key]['card_ids'].append(card['id'])
            pins[key]=sha(source);pins[str(target)]=sha(target)
    assert len(files)==2
    O.mkdir(exist_ok=False);result=dict(status='original_binary_fbx_color_layers_independently_inspected',finished_utc=datetime.now(timezone.utc).isoformat(),files=list(files.values()),input_pins=pins,unity_import_color_loss_claim=False,root_cause_qualified=False,source_mutations=0,full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(status=result['status'],files=[dict(path=Path(f['source']).name,color_layers=f['structure']['total_vertex_color_layers'],geometry_count=len(f['structure']['mesh_geometries'])) for f in files.values()])))
if __name__=='__main__':main()
