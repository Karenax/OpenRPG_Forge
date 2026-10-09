"""Bind measured component flags to converted scenes without changing any geometry or shader bytes."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct,copy
W=Path('F:/OpenRPG_ImportWork');OUT=W/'rocks-firepit-renderer-state-glb-v1';NATIVE=W/'rocks-firepit-native-component-state-v1/NATIVE-RESULT.v1.json';REVIEW=W/'rocks-firepit-native-component-state-reopen-v2/RESULT.v1.json'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def floatbits(v):return struct.pack('<f',v)
def load_json(p):
    with p.open('rb') as f:
        h=f.read(20);assert h[:4]==b'glTF' and struct.unpack('<I',h[4:8])[0]==2 and struct.unpack('<I',h[16:20])[0]==0x4e4f534a
        n=struct.unpack('<I',h[12:16])[0];assert n<=16*1024**2
        return json.loads(f.read(n)),20+n,h

def bind(doc,native):
    original=copy.deepcopy(doc);nodes={}
    for index,node in enumerate(doc['nodes']):
        source=node['extras']['unity_source_node'];assert source['path'] not in nodes;nodes[source['path']]=(index,source)
    root=nodes['root'][1];guid=root['gameobject']['guid'];prefab=next(p for p in native['prefabs'] if p['identity']['guid']==guid)
    expected={node['node_path']:node for node in prefab['nodes']};assert set(nodes)==set(expected)
    renderer_count=0
    for path,(index,source) in nodes.items():
        actual=expected[path];assert source['active_self']==actual['active_self']
        chain=[];walk=source
        while True:
            assert walk['path'] not in chain;chain.append(walk['path'])
            parent=walk['parent_path']
            if parent is None or parent=='':break
            assert parent in nodes;walk=nodes[parent][1]
        assert source['active_source_parent_chain']==all(nodes[p][1]['active_self'] for p in chain)
        source_renderers=source['renderers'];native_renderers=actual['renderers'];assert len(source_renderers)==len(native_renderers)
        native_ids={(v['identity']['guid'],v['identity']['file_id']):v for v in native_renderers};assert len(native_ids)==len(native_renderers)
        for renderer in source_renderers:
            identity=renderer['component'];key=(identity['guid'],identity['file_id']);assert key in native_ids and identity['exact_asset_identity_available']
            actual_renderer=native_ids[key];assert renderer['type']==actual_renderer['identity']['type']
            for field in ('enabled','shadow_casting_mode','receive_shadows'):assert renderer[field]==actual_renderer[field]
            assert 'force_rendering_off' not in renderer and type(actual_renderer['force_rendering_off']) is bool
            renderer['force_rendering_off']=actual_renderer['force_rendering_off'];renderer_count+=1
    groups=doc['extras']['original_lod_groups']
    native_groups={path:node['lod_group'] for path,node in expected.items() if node['lod_group']['identity']['guid']}
    assert len(groups)==len(native_groups)
    for group in groups:
        captured=native_groups[group['node_path']]
        assert floatbits(group['size'])==floatbits(captured['size'])
        assert [floatbits(v) for v in group['local_reference_point']]==[floatbits(v) for v in captured['local_reference_point']]
        assert group['fade_mode']==captured['fade_mode'] and group['animate_cross_fading']==captured['animate_cross_fading']
        assert len(group['levels'])==len(captured['levels'])
        for level,measured in zip(group['levels'],captured['levels']):
            for key in ('screen_relative_height','fade_transition_width'):assert floatbits(level[key])==floatbits(measured[key])
            assert [(v['guid'],v['file_id']) for v in level['renderer_components']]==[(v['guid'],v['file_id']) for v in measured['renderers']]
        assert 'enabled' not in group and 'source_component_identity' not in group
        group['enabled']=captured['enabled'];group['source_component_identity']=captured['identity']
    stripped=copy.deepcopy(doc)
    for node in stripped['nodes']:
        for renderer in node['extras']['unity_source_node']['renderers']:del renderer['force_rendering_off']
    for group in stripped['extras']['original_lod_groups']:
        del group['enabled'];del group['source_component_identity']
    assert stripped==original, 'Only native component flags and identities may be appended'
    return dict(nodes=len(nodes),renderers=renderer_count,lod_groups=len(groups))

def main():
    assert not OUT.exists();review=read(REVIEW);assert review['status']=='native_lod_renderer_and_hdrp_light_identity_reopened'
    pins=dict(review['input_pins']);pins[str(REVIEW)]=sha(REVIEW);pins[str(NATIVE)]=sha(NATIVE);pins[str(Path(__file__))]=sha(Path(__file__))
    for name,digest in pins.items():assert sha(Path(name))==digest,name
    native=read(NATIVE);sources=[]
    for folder,expected in [('rocks-firepit-complete-static-glb-v1','8e90e739e39f580d5173ffc7c1e6a3fe983a3611dfeefad914e8237ca0917c5e'),('firepit-complete-static-glb-v2','a8df029352894ef3f588f341141a3aa81ff446bd5268ecf205c4aeef37622ce6')]:
        found=list((W/folder).glob('*.candidate.glb'));assert len(found)==1;source=found[0];assert sha(source)==expected;pins[str(source)]=expected;sources.append(source)
    OUT.mkdir();(OUT/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,source_model_authored=False,vault_written=False),indent=2),'utf-8')
    results=[]
    for source in sources:
        doc,tail_offset,header=load_json(source);original=copy.deepcopy(doc);counts=bind(doc,native)
        refusals=[]
        for case in ('renderer_identity','authored_active_flag','original_shadow_mode','one_ulp_lod_threshold'):
            bad=copy.deepcopy(original)
            if case=='renderer_identity':next(n for n in bad['nodes'] if n['extras']['unity_source_node']['renderers'])['extras']['unity_source_node']['renderers'][0]['component']['guid']='0'*32
            elif case=='authored_active_flag':bad['nodes'][0]['extras']['unity_source_node']['active_self']=not bad['nodes'][0]['extras']['unity_source_node']['active_self']
            elif case=='original_shadow_mode':next(n for n in bad['nodes'] if n['extras']['unity_source_node']['renderers'])['extras']['unity_source_node']['renderers'][0]['shadow_casting_mode']=99
            else:
                level=bad['extras']['original_lod_groups'][0]['levels'][0];value=level['screen_relative_height'];level['screen_relative_height']=struct.unpack('<f',struct.pack('<I',struct.unpack('<I',floatbits(value))[0]+1))[0]
            try:bind(bad,native)
            except (AssertionError,StopIteration):refusals.append(case)
            else:raise AssertionError('Corrupt source state admitted')
        raw=json.dumps(doc,separators=(',',':'),ensure_ascii=False,allow_nan=False).encode('utf-8');raw+=b' '*((-len(raw))%4);assert len(raw)<=16*1024**2
        target=OUT/(source.stem+'.renderer-state.glb');tail_length=source.stat().st_size-tail_offset;total=20+len(raw)+tail_length
        old_tail=hashlib.sha256()
        with source.open('rb') as src,target.open('xb') as dst:
            src.seek(tail_offset);dst.write(header[:8]+struct.pack('<III',total,len(raw),0x4e4f534a)+raw)
            while block:=src.read(4*1024**2):old_tail.update(block);dst.write(block)
        reopened,new_offset,_=load_json(target);assert reopened==doc;new_tail=hashlib.sha256()
        with target.open('rb') as f:
            f.seek(new_offset)
            while block:=f.read(4*1024**2):new_tail.update(block)
        assert old_tail.digest()==new_tail.digest();assert sha(source)==pins[str(source)]
        results.append(dict(source=str(source),source_sha256=pins[str(source)],target=str(target),target_sha256=sha(target),target_bytes=target.stat().st_size,unchanged_binary_chunks_sha256=new_tail.hexdigest(),negative_controls_refused=refusals,**counts))
    for name,digest in pins.items():assert sha(Path(name))==digest,name
    result=dict(status='native_renderer_flags_and_lod_group_identities_bound_without_geometry_or_shader_changes',finished_utc=datetime.now(timezone.utc).isoformat(),rows=results,source_pins=pins,nodes=sum(r['nodes'] for r in results),renderers=sum(r['renderers'] for r in results),lod_groups=sum(r['lod_groups'] for r in results),source_model_authored=False,vault_written=False,actual_source_renderer_integrated=False,actual_gpu_rendered=False,actual_editor_global_qualified=False,full_import=False)
    (OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ('rows','source_pins')}))
if __name__=='__main__':main()
