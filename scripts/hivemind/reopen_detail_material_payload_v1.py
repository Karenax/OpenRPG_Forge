"""Independent reconstruction of embedded source files and native mip chains."""
from pathlib import Path
import copy,datetime,hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'detail-autonomous-material-payload-v1'
def read(p):return json.loads(p.read_text('utf-8'))
def digest(b):return hashlib.sha256(b).hexdigest()
def sha(p):return digest(p.read_bytes())
def verify(doc,data):
    assert len(data)==doc['buffers'][0]['byteLength'] and digest(data)==doc['binary_sha256']
    def blob(b):
        v=doc['bufferViews'][b['buffer_view']];assert v['buffer']==0
        at=v.get('byteOffset',0);size=v['byteLength'];assert at>=0 and at+size<=len(data)
        raw=data[at:at+size];assert digest(raw)==b['sha256'];return raw
    for b in doc['roles'].values():blob(b)
    assert blob(doc['scalar_parameters']['values'])==struct.pack('<ff',1.,1.)+bytes(248)
    assert doc['scalar_parameters']['names']==['native_default_mask_r','native_default_mask_g']
    native=read(W/'detail-texture-gpu-v1/INPUT.v1.json')['rows'];kernel=read(W/'detail-native-default-kernel-v1/RESULT.v1.json')
    total=0
    for slot,(t,n,binding) in enumerate(zip(doc['textures'],native,kernel['source_texture_bindings'],strict=True)):
        assert t['slot']==slot and t['source_guid']==n['unity_guid']==binding['guid']
        for role in ['png','source_meta']:
            suffix='.png' if role=='png' else '.meta'
            original=next(f for f in binding['source_files'] if f['path'].endswith(suffix))
            assert blob(t[role])==Path(original['path']).read_bytes()
        assert t['normal_decode_mode']==('rg_or_ag' if slot==1 else None)
        desc=t['imported_storage'];storage=blob(desc['storage'])
        assert storage==Path(n['storage']).read_bytes()
        capture=blob(desc['capture']);assert capture==(W/'detail-native-texture-storage-v1/export/RESULT.json').read_bytes()
        case=json.loads(capture)['cases'][desc['capture_case_index']]
        assert desc['capture_case_index']==slot and desc['width']==case['width']==n['width'] and desc['height']==case['height']==n['height']
        assert desc['mips']==[{k:v for k,v in m.items() if k!='level'} for m in n['mips']]
        for mip in desc['mips']:
            assert digest(storage[mip['offset']:mip['offset']+mip['bytes']])==mip['sha256'];total+=1
    assert total==24 and len(doc['textures'])==2
    assert doc['pipeline_state_not_fabricated'] and not doc['glb_created'] and not doc['renderer_installed']
    return len(doc['roles']),total
def main():
    out=ROOT/'INDEPENDENT-REOPEN.v1.json';assert not out.exists()
    result=read(ROOT/'RESULT.v1.json');path=ROOT/'PAYLOAD.v1.json';data=(ROOT/'material-payload.bin').read_bytes()
    assert sha(path)==result['manifest_sha256'] and digest(data)==result['binary_sha256']
    for p,h in result['input_pins'].items():assert sha(Path(p))==h,p
    doc=read(path);components,mips=verify(doc,data);refused=[]
    for name in ['corrupt_storage','wrong_normal_mode','wrong_capture_case','changed_mip_offset','changed_scalar_packet','wrong_source_identity']:
        d=copy.deepcopy(doc);b=bytearray(data)
        if name=='corrupt_storage':
            v=d['bufferViews'][d['textures'][0]['imported_storage']['storage']['buffer_view']];b[v['byteOffset']]^=1
        elif name=='wrong_normal_mode':d['textures'][1]['normal_decode_mode']='rgb'
        elif name=='wrong_capture_case':d['textures'][0]['imported_storage']['capture_case_index']=1
        elif name=='changed_mip_offset':d['textures'][1]['imported_storage']['mips'][2]['offset']+=16
        elif name=='changed_scalar_packet':
            v=d['bufferViews'][d['scalar_parameters']['values']['buffer_view']];b[v['byteOffset']]^=1
        else:d['textures'][0]['source_guid']=d['textures'][1]['source_guid']
        try:verify(d,bytes(b))
        except (AssertionError,IndexError,KeyError):refused.append(name)
        else:raise RuntimeError('Mutation accepted: '+name)
    receipt=dict(status='embedded_detail_material_source_bytes_and_native_mips_reopened',
        components_verified=components,textures_verified=2,mips_verified=mips,refused_mutations=refused,
        payload_sha256=sha(path),binary_sha256=digest(data),sources_unchanged=True,
        pipeline_state_qualified=False,gpu_rendered=False,studio_verified=False,full_import=False,
        verifier_sha256=sha(Path(__file__)),finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    out.write_text(json.dumps(receipt,indent=2),'utf-8');print(json.dumps(receipt))
if __name__=='__main__':main()
