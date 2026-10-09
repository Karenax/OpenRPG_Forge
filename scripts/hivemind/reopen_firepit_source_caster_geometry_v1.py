from pathlib import Path
import json,hashlib,struct,datetime
W=Path('F:/OpenRPG_ImportWork');R=W/'firepit-source-caster-geometry-v1';O=W/'firepit-source-caster-geometry-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def bits(x):return struct.pack('<f',x)
assert not O.exists()
prep=read(R/'PREPARED.v1.json');run=read(R/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
pins={**prep['input_pins'],**prep['private_before_pins']}
for name,h in pins.items():assert sha(Path(name))==h,name
casepath=W/'firepit-source-shadow-raster-case-v1/CASE.v1.json';case=read(casepath);fixture=Path(case['fixture']);assert sha(fixture)==case['fixture_sha256']
b=fixture.read_bytes();length=struct.unpack_from('<I',b,12)[0];g=json.loads(b[20:20+length]);blob=b[28+length:]
def accessor(i):
 a=g['accessors'][i];assert 'sparse' not in a and not a.get('normalized',False)
 v=g['bufferViews'][a['bufferView']];assert v.get('buffer',0)==0
 typ={5126:'f',5125:'I',5123:'H'}[a['componentType']];count={'SCALAR':1,'VEC3':3,'VEC4':4}[a['type']];fmt='<'+typ*count;size=struct.calcsize(fmt);step=v.get('byteStride',size);offset=v.get('byteOffset',0)+a.get('byteOffset',0)
 return [struct.unpack_from(fmt,blob,offset+j*step) for j in range(a['count'])]
checks=[];vertices=triangles=0
for view in range(2):
 srcpath=R/f'SOURCE-CASTER-GEOMETRY-{view}.json';capture=read(srcpath);assert capture['lod0_forced_diagnostic_only'] and not capture['source_gpu_vertex_buffer_readback']
 rows=capture['renderers'];assert len(rows)==4
 assert [x['lod_membership'] for x in rows]==[[0],[1],[2],[3]]
 assert [x['is_visible'] for x in rows]==[True,False,False,False]
 for lod,row in enumerate(rows):
  mesh=next(m for m in g['meshes'] if m['name']==row['name']);assert len(mesh['primitives'])==len(row['submeshes'])==1
  primitive=mesh['primitives'][0];assert primitive['mode']==4 and row['submeshes'][0]['topology']=='Triangles'
  pos=accessor(primitive['attributes']['POSITION']);norm=accessor(primitive['attributes']['NORMAL'])
  assert len(pos)==len(norm)==len(row['positions'])==len(row['normals'])
  posbad=sum(bits(a)!=bits(b) for v,e in zip(pos,row['positions']) for a,b in zip(v,(-e['x'],e['y'],e['z'])))
  normbad=sum(bits(a)!=bits(b) for v,e in zip(norm,row['normals']) for a,b in zip(v,(-e['x'],e['y'],e['z'])))
  original=row['submeshes'][0]['indices'];assert len(original)%3==0
  indices=[i[0] for i in accessor(primitive['indices'])];expected=[v for i in range(0,len(original),3) for v in [original[i],original[i+2],original[i+1]]]
  assert len(indices)==len(expected);indexbad=sum(a!=b for a,b in zip(indices,expected))
  assert posbad==normbad==indexbad==0
  assert row['enabled'] and row['active_in_hierarchy'] and not row['force_rendering_off'] and row['shadow_casting_mode']==('On' if view==0 else 'Off')
  props=dict(zip(row['materials'][0]['property_names'],row['materials'][0]['property_values']));assert props['_CullMode']==2 and props['_DoubleSidedEnable']==0 and props['_DepthOffsetEnable']==0 and props['_AlphaCutoffEnable']==0
  node=next(n for n in g['nodes'] if n['name']==row['name']);source_matrix=node['extras']['unity_source_node']['source_local_to_world_matrix_row_major']
  assert all(bits(row['local_to_world'][c*4+r])==bits(source_matrix[r*4+c]) for r in range(4) for c in range(4))
  checks.append(dict(view=view,lod=lod,mesh_identity=row['mesh_identity'],vertices=len(pos),triangles=len(indices)//3,position_f32_bit_mismatches=posbad,normal_f32_bit_mismatches=normbad,triangle_winding_index_mismatches=indexbad,source_depth_offset_enabled=False,source_cull_mode=2))
  if view==0:vertices+=len(pos);triangles+=len(indices)//3
 for rel in [f'images/{view}.png',f'linear-{view}.rgba16f']:
  a=R/rel;c=W/'firepit-source-shadow-raster-v6'/rel;assert sha(a)==sha(c);pins[str(a)]=sha(a);pins[str(c)]=sha(c)
 pins[str(srcpath)]=sha(srcpath)
for file in [Path(__file__),casepath,fixture,R/'PREPARED.v1.json',R/'RUN-RESULT.v1.json',W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Assets/Editor/SourceShadowRasterV6.cs',W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Assets/Editor/SourceShadowRasterV6.compute']:
 pins[str(file)]=sha(file)
result=dict(status='source_caster_static_mesh_positions_normals_winding_and_face_policy_reopened',finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),checks=checks,mesh_levels_verified=4,views_verified=2,distinct_vertices_verified=vertices,distinct_triangles_verified=triangles,source_dependencies_unchanged=24,original_png_hdr_pairs_unchanged=4,source_lod0_visible_other_lods_not_visible_in_diagnostic=True,source_depth_offset_and_alpha_clip_disabled=True,coordinate_mapping='Reflect X and reverse each triangle order, exactly as the retained converter; no geometry repair or fitting',native_actual_vertex_output_gpu_verified=False,source_actual_vertex_output_gpu_verified=False,full_shadow_fidelity_qualified=False,editor_visual_verified=False,full_import=False,complete_assets_delivered_increment=0,next_action='Verify actual source/native shadow vertex outputs and effective depth writes; static mesh buffers, declared transforms and source LOD visibility do not identify the remaining depth discrepancy.',input_pins=pins)
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','checks']}))
