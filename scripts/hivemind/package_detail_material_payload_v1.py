"""Stage actual material bytes for scene assembly, without inventing render state."""
from pathlib import Path
import datetime,hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'detail-autonomous-material-payload-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def digest(b):return hashlib.sha256(b).hexdigest()
def sha(p):return digest(p.read_bytes())
def main():
    assert not ROOT.exists()
    pins={};views=[];data=bytearray();roles={}
    def embed(role,path,expected=None):
        path=Path(path);payload=path.read_bytes();h=digest(payload)
        if expected is not None:assert h==expected,str(path)
        pins[str(path)]=h
        data.extend(b'\0'*(-len(data)%4));index=len(views)
        views.append(dict(buffer=0,byteOffset=len(data),byteLength=len(payload)))
        data.extend(payload);roles[role]=dict(buffer_view=index,sha256=h)
        return roles[role]
    proof_names=['detail-native-lit-fragments-v1/INDEPENDENT-REOPEN.v1.json',
        'detail-normal-decoder-independent-reopen-v1/RESULT.v1.json',
        'detail-native-texture-storage-v1/TEXTURE-REOPEN.v1.json',
        'detail-native-default-bevy-vertex-v2/RESULT.v1.json',
        'detail-authored-effective-state-v2/RESULT.v1.json',
        'detail-original-render-contracts-v1/RESULT.v1.json',
        'rocks-firepit-scene-bindings-v1/RESULT.v1.json',
        'source-graph-scalar-transport-native-v6/RUN-RESULT.v1.json']
    proofs=[read(W/p) for p in proof_names]
    lit,normal,textures,scalars,effective,authored,assembly,tests=proofs
    assert lit['native_lit_programs_reopened']==1 and lit['actual_positive_compositions_reopened']==8
    assert normal['normal_mips_verified']==12 and normal['negative_controls_passed']==5
    assert textures['source_texture_storage_captures_verified']==2 and textures['mips_verified']==24
    assert not effective['source_pass_label_resolution_qualified']
    assert assembly['meshes_reopened']==36
    assert tests['status']=='actual_scalar_transport_and_previous_source_graph_tests_passed'
    for i,rel in enumerate(proof_names):embed('proof-'+str(i),W/rel)
    for role in ['forward','prepass']:
        row=lit['rows'][0];embed(role,row[role],row[role+'_sha256'])
    stage=read(W/'detail-native-default-stages-v1/RESULT.v1.json')
    embed('source-program',stage['program'],stage['program_sha256'])
    embed('uniform-120',scalars['uniform_packet'],scalars['uniform_packet_sha256'])
    packet=Path(scalars['uniform_packet']).read_bytes()
    assert packet==struct.pack('<ff',1.,1.)+bytes(248)
    kernel=read(W/'detail-native-default-kernel-v1/RESULT.v1.json')
    gpu_path=W/'detail-texture-gpu-v1/INPUT.v1.json';gpu=read(gpu_path);embed('gpu-storage-manifest',gpu_path)
    capture=W/'detail-native-texture-storage-v1/export/RESULT.json'
    assert sha(capture)==textures['unity_result_sha256']
    capture_blob=embed('texture-capture',capture, textures['unity_result_sha256'])
    descriptors=[]
    for slot,(binding,row) in enumerate(zip(kernel['source_texture_bindings'],gpu['rows'],strict=True)):
        assert binding['guid']==row['unity_guid']
        files=binding['source_files'];png=next(f for f in files if f['path'].endswith('.png'))
        meta=next(f for f in files if f['path'].endswith('.meta'))
        png_blob=embed(f'texture-{slot}-png',png['path'],png['sha256'])
        meta_blob=embed(f'texture-{slot}-meta',meta['path'],meta['sha256'])
        storage=embed(f'texture-{slot}-storage',row['storage'],row['storage_sha256'])
        raw=Path(row['storage']).read_bytes()
        for mip in row['mips']:assert digest(raw[mip['offset']:mip['offset']+mip['bytes']])==mip['sha256']
        s=row['resolved_sampler']
        descriptor=dict(schema_version=1,storage=storage,capture=capture_blob,capture_case_index=slot,
            width=row['width'],height=row['height'],format=['bc3_rgba_unorm_srgb','bc3_rgba_unorm'][slot],
            mips=[{k:v for k,v in m.items() if k!='level'} for m in row['mips']],
            sampler=dict(filter_mode=s['filter_mode'],wrap=[s['wrap_u'],s['wrap_v'],s['wrap_w']],
                anisotropy=s['anisotropy'],mip_bias=s['mip_bias'],source_mipmaps_enabled=row['source_mipmaps_enabled']))
        descriptors.append(dict(slot=slot,source_guid=binding['guid'],png=png_blob,source_meta=meta_blob,
            imported_storage=descriptor,normal_decode_mode='rg_or_ag' if slot==1 else None))
    source=authored['rows'][0]
    embed('original-material',source['original_material'],source['original_material_sha256'])
    embed('original-graph',source['original_graph'],source['original_graph_sha256'])
    for name in ['LICENSE.Unity.md','LICENSE.Bevy.MIT.txt']:
        embed(name,W/'detail-native-lit-fragments-v1'/name)
    for path,h in pins.items():assert sha(Path(path))==h,path
    manifest=dict(schema='openrpg.staged-material-payload.v1',material_guid=source['material_guid'],
        buffers=[dict(byteLength=len(data))],bufferViews=views,roles=roles,textures=descriptors,
        scalar_parameters=dict(names=['native_default_mask_r','native_default_mask_g'],values=roles['uniform-120']),
        authored_state_receipt=roles['proof-5'],effective_state_receipt=roles['proof-4'],
        unresolved_admission=['primary_and_secondary_attachment_state','source_disabled_pass_label_resolution',
            'stencil_and_pipeline_role_mapping','complete_prefab_renderer_and_gpu_validation'],
        pipeline_state_not_fabricated=True,glb_created=False,renderer_installed=False,full_import=False)
    ROOT.mkdir();binary=ROOT/'material-payload.bin';binary.write_bytes(data)
    manifest['binary_sha256']=sha(binary);manifest_path=ROOT/'PAYLOAD.v1.json'
    manifest_path.write_text(json.dumps(manifest,indent=2),'utf-8')
    result=dict(status='actual_detail_material_payload_embedded_for_scene_assembly',textures=2,mips=24,
        forward_and_prepass_embedded=True,measured_scalar_bytes=256,source_shader_and_material_embedded=True,
        binary=str(binary),binary_sha256=sha(binary),manifest=str(manifest_path),manifest_sha256=sha(manifest_path),
        component_views=len(views),bytes=len(data),input_pins=pins,full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins']}))
if __name__=='__main__':main()
