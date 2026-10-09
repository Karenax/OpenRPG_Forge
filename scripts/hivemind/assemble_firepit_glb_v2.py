"""Assemble genuine complete static scene carriers; render/LOD/VFX admission remains explicit."""
from pathlib import Path
import datetime,hashlib,json,struct
from assemble_native_villa_delivery import pack_glb
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-complete-static-glb-v2'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def digest(data):return hashlib.sha256(data).hexdigest()
def sha(p):return digest(p.read_bytes())
def main():
    assert not ROOT.exists()
    pins={}
    def load(rel):
        p=W/rel;pins[str(p)]=sha(p);return read(p)
    assembly=load('rocks-firepit-scene-bindings-v1/RESULT.v1.json');assert assembly['meshes_reopened']==36
    pins.update(assembly['input_pins'])
    def merged(paths):return {r['material_guid']:r for rel in paths for r in load(rel)['rows']}
    programs=merged(['delta-native-lit-fragments-rgorag-v2/INDEPENDENT-REOPEN.v1.json','delta-rocks-firepit-native-lit-v1/INDEPENDENT-REOPEN.v1.json','detail-native-lit-fragments-v1/INDEPENDENT-REOPEN.v1.json'])
    kernels=merged(['delta-vector-node-wgsl-v3/RESULT.v1.json','nested-prefab-delta-material-wgsl-v1/RESULT.v1.json'])
    detail=load('detail-native-default-kernel-v1/RESULT.v1.json');kernels[detail['material_guid']]=detail
    contracts=merged(['delta-foliage-render-contracts-v1/RESULT.v1.json','delta-rocks-firepit-render-contracts-v1/RESULT.v1.json','detail-original-render-contracts-v1/RESULT.v1.json'])
    stages=merged(['delta-vector-native-stages-v1/RESULT.v1.json','delta-rocks-firepit-native-stages-v1/RESULT.v1.json'])
    ds=load('detail-native-default-stages-v1/RESULT.v1.json');stages[ds['material_guid']]=ds
    native={c['material_guid']:c for rel in ['unity-delta-foliage-native-materials-v2/NATIVE-RESULT.v1.json','unity-delta-remaining-native-materials-v2/NATIVE-RESULT.v1.json','detail-native-material-state-v1/NATIVE-RESULT.v1.json'] for c in load(rel)['cases']}
    reopened=load('firepit-static-material-native-reimport-v3/RUN-RESULT.v1.json')
    assert reopened['status']=='static_firepit_original_shader_and_native_passes_reopened' and reopened['actual_unity_exit_code']==0
    load('firepit-static-material-native-reimport-v3/REOPEN.v1.json')
    fresh=load('firepit-static-material-native-reimport-v3/NATIVE-RESULT.v1.json')['cases']
    assert len(fresh)==1 and fresh[0]['error']=='' and fresh[0]['shader_supported'] and fresh[0]['compile_passes_executed']
    native[fresh[0]['material_guid']]=fresh[0]
    pins[str(Path(__file__).with_name('assemble_rocks_firepit_glb_v1.py'))]=sha(Path(__file__).with_name('assemble_rocks_firepit_glb_v1.py'))
    storage={}
    format_map={'RGBA_DXT5_SRGB':'bc3_rgba_unorm_srgb','RGBA_DXT5_UNorm':'bc3_rgba_unorm','RGBA_DXT1_SRGB':'bc1_rgba_unorm_srgb','RGBA_DXT1_UNorm':'bc1_rgba_unorm','R_BC4_UNorm':'bc4_r_unorm','R8G8B8_UNorm':'rgb8_unorm','R8G8B8A8_UNorm':'rgba8_unorm'}
    for rel in ['delta-foliage-texture-gpu-v3/INPUT.v1.json','delta-rocks-firepit-texture-gpu-v1/INPUT.v1.json','detail-texture-gpu-v1/INPUT.v1.json']:
        for row in load(rel)['rows']:
            raw_path=Path(row['original_storage']);capture_path=raw_path.parent.parent/'RESULT.json'
            capture=read(capture_path);ordinal=int(raw_path.parent.name);case=capture['cases'][ordinal]
            assert case['actual_guid']==row['unity_guid'] and case['expected_identity_matches'] and not case['error']
            assert sha(raw_path)==row['original_storage_sha256']
            pins[str(capture_path)]=sha(capture_path);pins[str(raw_path)]=sha(raw_path)
            raw=raw_path.read_bytes();offset=0;mips=[]
            for m in case['mips']:
                piece=raw[offset:offset+m['bytes']];assert len(piece)==m['bytes']
                mip_file=raw_path.parent/('mip-'+str(m['level']).zfill(2)+'.bin')
                assert piece==mip_file.read_bytes();pins[str(mip_file)]=digest(piece)
                mips.append(dict(width=m['width'],height=m['height'],offset=offset,bytes=m['bytes'],sha256=digest(piece)));offset+=m['bytes']
            assert offset==len(raw)==case['raw_whole_bytes']
            entry=dict(row=row,raw_path=raw_path,capture_path=capture_path,ordinal=ordinal,case=case,mips=mips,format=format_map[case['graphics_format']])
            assert row['unity_guid'] not in storage;storage[row['unity_guid']]=entry
    normal_guids=set()
    for rel in ['delta-foliage-normal-decoder-gpu-v1/INPUT.v1.json','delta-rocks-firepit-normal-decoder-gpu-v1/INPUT.v1.json','detail-normal-decoder-gpu-v1/INPUT.v1.json']:
        normal_guids.update(r['unity_guid'] for r in load(rel)['rows'])
    # Actual decoder and storage proofs remain distinct from a whole material draw.
    for rel in ['delta-foliage-normal-decoder-gpu-v1/RUN-RESULT.v1.json','delta-rocks-firepit-normal-decoder-gpu-v1/RUN-RESULT.v1.json','detail-normal-decoder-independent-reopen-v1/RESULT.v1.json','detail-material-canonical-storage-v1/RESULT.v1.json']:
        load(rel)
    scalar=load('detail-native-default-bevy-vertex-v2/RESULT.v1.json')
    expected=set(g for row in assembly['rows'] for g in row['material_guids']);assert len(expected)==9
    assert expected.issubset(programs) and expected.issubset(kernels) and expected.issubset(native)
    ROOT.mkdir();results=[]
    for scene in assembly['rows']:
        if scene['asset_id']!='hivemind.native-american-village.prefab-sm-firepit-01-abc00000000014341674918090329798':continue
        data=bytearray();views=[];accessors=[];embedded={};materials=[];meshes=[];mesh_keys={};material_index={};geometry_sources={}
        def embed(path,expected_hash=None):
            path=Path(path);raw=path.read_bytes();h=digest(raw)
            if expected_hash is not None:assert h==expected_hash,str(path)
            pins[str(path)]=h
            if h not in embedded:
                data.extend(bytes(-len(data)%4));embedded[h]=len(views);views.append(dict(buffer=0,byteOffset=len(data),byteLength=len(raw)));data.extend(raw)
            return dict(buffer_view=embedded[h],sha256=h)
        def accessor(path,width,count,component=5126,bounds=False):
            raw=Path(path).read_bytes();assert len(raw)==count*width*4
            b=embed(path);a=dict(bufferView=b['buffer_view'],componentType=component,count=count,type={1:'SCALAR',2:'VEC2',3:'VEC3',4:'VEC4'}[width])
            if bounds:
                values=list(struct.iter_unpack('<fff',raw));a.update(min=[min(v[i] for v in values) for i in range(3)],max=[max(v[i] for v in values) for i in range(3)])
            accessors.append(a);return len(accessors)-1
        for guid in scene['material_guids']:
            authored=contracts[guid];program=programs[guid];kernel=kernels[guid];capture=native[guid]
            assert not capture['error'] and capture['shader_supported'] and capture['compile_passes_executed'],guid
            np={p['name']:p['native_float'] for p in capture['properties'] if p['type'] in ['Float','Range','Int']}
            f=authored['source_floats']
            def value(name):
                assert name in np,name
                if name in f:assert float(f[name])==np[name],(guid,name,f[name],np[name])
                return np[name]
            textures=[];modes=[None]*8
            for slot,binding in enumerate(kernel['source_texture_bindings']):
                s=storage[binding['guid']];case=s['case'];files=binding['source_files']
                png=next(x for x in files if x['path'].endswith('.png'));meta=next(x for x in files if x['path'].endswith('.meta'))
                assert binding['guid'] in s['row']['asset_id']
                sampler=dict(filter_mode=case['filter_mode'],wrap=[case['wrap_u'],case['wrap_v'],case['wrap_w']],anisotropy=case['anisotropy'],mip_bias=case['mip_bias'],source_mipmaps_enabled=s['row']['source_mipmaps_enabled'])
                descriptor=dict(schema_version=1,storage=embed(s['raw_path'],s['row']['original_storage_sha256']),capture=embed(s['capture_path']),capture_case_index=s['ordinal'],width=case['width'],height=case['height'],format=s['format'],mips=s['mips'],sampler=sampler)
                textures.append(dict(png=embed(png['path'],png['sha256']),source_meta=embed(meta['path'],meta['sha256']),source_meta_sha256=meta['sha256'],is_srgb=case['importer_srgb'],source_mipmaps=sampler['source_mipmaps_enabled'],filter_mode=sampler['filter_mode'],wrap=sampler['wrap'],anisotropy=sampler['anisotropy'],mip_bias=sampler['mip_bias'],imported_storage=descriptor))
                if binding['requires_normal_decode']:
                    assert binding['guid'] in normal_guids;modes[slot]='rg_or_ag'
            assert len(textures)==stages[guid]['texture_count']
            wire=dict(schema_version=5 if guid==detail['material_guid'] else 4,source_program_sha256=stages[guid]['program_sha256'],source_material_sha256=authored['original_material_sha256'],
                forward=embed(program['forward'],program['forward_sha256']),prepass=embed(program['prepass'],program['prepass_sha256']),libraries=[],textures=textures,
                state=dict(color_write_mask=15,color_blend=[int(value('_SrcBlend')),int(value('_DstBlend'))],alpha_blend=[int(value('_AlphaSrcBlend')),int(value('_AlphaDstBlend'))],
                    forward_cull=int(value('_CullModeForward')),prepass_cull=int(value('_CullMode')),depth_compare=int(value('_ZTestDepthEqualForOpaque')),write_depth=value('_ZWrite')==1,
                    transparent=authored['source_keyword_transparent'],alpha_test=authored['source_keyword_alpha_test'],alpha_to_coverage=authored['source_keyword_alpha_to_mask'],render_queue=capture['render_queue'],disabled_passes=authored['source_disabled_shader_passes']),
                requires_original_vertex_color=False,normal_decode_modes=modes)
            if guid==detail['material_guid']:
                wire['scalar_parameters']=dict(names=['native_default_mask_r','native_default_mask_g'],values=embed(scalar['uniform_packet'],scalar['uniform_packet_sha256']))
            # Preserve original author files and native observations without reducing
            # pipeline-global parameters to invented material floats.
            extras=dict(source_guid=guid,original_material=embed(authored['original_material'],authored['original_material_sha256']),original_graph=embed(authored['original_graph'],authored['original_graph_sha256']),source_stage=embed(stages[guid]['program'],stages[guid]['program_sha256']),authored_render_state=authored,native_material_observation=capture,
                resolved_native_sampler_transport=True,original_importer_version_parity_qualified=False,pipeline_roles_qualified=False)
            material_index[guid]=len(materials);materials.append(dict(name=Path(authored['original_material']).stem,extensions={'OPENRPG_source_graph':wire},extras=extras))
        bindings={b['node_path']:b for b in scene['static_renderer_bindings']};assert len(bindings)==len(scene['static_renderer_bindings'])
        nodes=[];indices={n['source_node']['path']:i for i,n in enumerate(scene['nodes'])}
        for entry in scene['nodes']:
            node=entry['source_node'];target=dict(name=node['name'],matrix=entry['target_local_matrix_column_major'],extras=dict(unity_source_node=node,source_world_matrix_target=entry['target_source_world_matrix_column_major'],active_and_renderer_state_admission_pending=True))
            if node['path'] in bindings:
                b=bindings[node['path']];directory=Path(b['geometry_derivative']);gp=directory/'GEOMETRY.v1.json';assert sha(gp)==b['geometry_receipt_sha256'];pins[str(gp)]=sha(gp)
                g=read(gp);m=g['metadata'];count=m['vertices'];assert m['normals_count']==m['tangents_count']==m['uv_counts'][0]==count
                key=(b['source_mesh_identity']['guid'],b['source_mesh_identity']['file_id'],tuple(x['guid'] for x in b['original_material_slots']))
                if key not in mesh_keys:
                    raw={Path(p).name:Path(p) for p in g['source_stream_pins']}
                    for path,h in g['source_stream_pins'].items():assert sha(Path(path))==h; pins[path]=h
                    for name,desc in g['derived_streams'].items():assert sha(directory/name)==desc['sha256'];pins[str(directory/name)]=desc['sha256']
                    attrs={'POSITION':accessor(directory/'positions.target.f32',3,count,bounds=True),'NORMAL':accessor(directory/'normals.target.f32',3,count),'TANGENT':accessor(directory/'tangents.target.f32',4,count),
                        '_OPENRPG_SOURCE_NORMAL':accessor(raw['normals.f32'],3,count),'_OPENRPG_SOURCE_TANGENT':accessor(raw['tangents.f32'],4,count)}
                    for channel,c in enumerate(m['uv_counts']):
                        if c:
                            assert c==count;attrs['TEXCOORD_'+str(channel)]=accessor(directory/('uv'+str(channel)+'.target.f32'),2,count)
                            attrs['_OPENRPG_SOURCE_UV'+str(channel)]=accessor(raw['uv'+str(channel)+'.f32'],4,count)
                    if m['colors_count']:attrs['_OPENRPG_SOURCE_COLOR']=accessor(raw['colors.f32'],4,count)
                    primitives=[]
                    assert m['submeshes']==len(b['original_material_slots'])
                    for sub in range(m['submeshes']):
                        path=directory/('submesh-'+str(sub)+'.target.u32');n=path.stat().st_size//4
                        primitives.append(dict(attributes=attrs.copy(),indices=accessor(path,1,n,5125),material=material_index[b['original_material_slots'][sub]['guid']],mode=4))
                    mesh_keys[key]=len(meshes);meshes.append(dict(name=m['mesh_name'],primitives=primitives,extras=dict(original_mesh_identity=b['source_mesh_identity'],geometry_receipt_sha256=sha(gp))))
                    geometry_sources[str(mesh_keys[key])]=str(directory)
                target['mesh']=mesh_keys[key]
            nodes.append(target)
        roots=[]
        for i,entry in enumerate(scene['nodes']):
            parent=entry['source_node']['parent_path']
            if parent is None or parent=='':roots.append(i)
            else:nodes[indices[parent]].setdefault('children',[]).append(i)
        assert len(roots)==1
        doc=dict(asset=dict(version='2.0',generator='OpenRPG source-preserving native scene assembly'),extensionsUsed=['OPENRPG_source_graph'],extensionsRequired=['OPENRPG_source_graph'],buffers=[dict(byteLength=len(data))],bufferViews=views,accessors=accessors,materials=materials,meshes=meshes,nodes=nodes,scenes=[dict(nodes=roots)],scene=0,
            extras=dict(asset_id=scene['asset_id'],unity_captured_vertices=True,source_frame_binding=dict(target_object_from_source_object=[-1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1],source_object_from_target_object=[-1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]),original_lod_groups=scene['original_lod_groups'],deferred_renderers=scene['deferred_renderers'],source_scene_root=scene['original_root'],full_import=False,
                lod_active_state_pipeline_and_particle_runtime_pending=True))
        path=ROOT/(scene['asset_id']+'.candidate.glb');path.write_bytes(pack_glb(doc,bytes(data)))
        results.append(dict(asset_id=scene['asset_id'],glb=str(path),sha256=sha(path),bytes=path.stat().st_size,nodes=len(nodes),static_renderers=len(bindings),submesh_instances=scene['declared_submesh_draws'],materials=len(materials),mesh_variants=len(meshes),geometry_sources=geometry_sources,deferred_effect_renderers=len(scene['deferred_renderers']),canonical_reader_accepted=False,gpu_rendered=False,studio_verified=False,full_import=False))
        (ROOT/'SCENE-RESULT.v1.json').write_text(json.dumps(results[-1],indent=2),'utf-8')
    assert len(results)==1
    for path,h in pins.items():assert sha(Path(path))==h,path
    pins[str(Path(__file__))]=sha(Path(__file__))
    result=dict(status='firepit_full_hierarchy_static_scene_carrier_assembled_particles_pending',rows=results,input_pins=pins,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(results))
if __name__=='__main__':main()
