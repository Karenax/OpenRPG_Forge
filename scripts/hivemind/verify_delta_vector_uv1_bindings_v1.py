"""Reopen exact captured UV1 bytes for every renderer using the six new kernels."""
from pathlib import Path
import datetime,json,hashlib,struct,math
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'delta-vector-uv1-binding-reopen-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists()
 plan_path=W/'nested-prefab-scene-bindings-v2/RESULT.v1.json';plan=read(plan_path)
 kernel_path=W/'delta-vector-node-converter-v3/INDEPENDENT-REOPEN.v1.json';kernels=read(kernel_path)
 topology_path=W/'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json';topology=read(topology_path);irs={r['material_guid']:r for r in topology['rows']}
 wanted={r['material_guid'] for r in kernels['rows']};assert len(wanted)==6
 pins={str(p):sha(p) for p in [plan_path,kernel_path,topology_path,Path(__file__)]};consumption=[]
 for guid in sorted(wanted):
  row=irs[guid];p=Path(row['output']);assert sha(p)==row['output_sha256'];pins[str(p)]=sha(p);ir=read(p)
  uvnodes=[n for n in ir['nodes'] if n['opcode']=='UVNode' and n['source_parameters']['m_OutputChannel']==1];assert len(uvnodes)==1
  consumers=[n for n in ir['nodes'] if any(v['value'].get('ref')==uvnodes[0]['id'] for v in n['inputs'])]
  assert consumers and all(n['opcode']=='SplitNode' and n['output_slot_id']==2 for n in consumers),'Full UV1 fetch padding would require a separate native GPU observation'
  consumption.append(dict(material_guid=guid,used_source_channel=1,used_component='y',full_vec4_shader_fetch_padding_qualified=False))
 geometries={};rows=[]
 for asset in plan['rows']:
  for binding in asset['static_renderer_bindings']:
   hits=sorted({m.get('guid') for m in binding['original_material_slots']} & wanted)
   if not hits:continue
   base=Path(binding['geometry_derivative']);gpath=base/'GEOMETRY.v1.json';g=read(gpath);mesh=binding['source_mesh_identity']
   reopening_path=base/'REOPEN.v1.json';reopening=read(reopening_path)
   assert sha(gpath)==binding['geometry_receipt_sha256']==reopening['geometry_receipt_sha256'] and g['original_streams_unchanged']
   assert reopening['native_capture_terminal_verified'] and reopening['source_mesh_identity']==g['expected_identity']
   assert any(alias['asset_id']==asset['asset_id'] and alias['node_path']==binding['node_path'] for alias in reopening['prefab_instance_aliases'])
   pins[str(reopening_path)]=sha(reopening_path)
   assert g['expected_identity']=={'guid':mesh['guid'],'file_id':mesh['file_id']}
   if str(base) not in geometries:
    meta=g['metadata'];count=meta['vertices'];assert meta['uv_counts'][1]==count>0
    format=[f for f in meta['vertex_formats'] if f['attribute']=='TexCoord1'];assert len(format)==1 and format[0]['format']=='Float32' and format[0]['dimension']>=2
    p=base/'uv1.source.f32';expected=g['derived_streams'][p.name];payload=p.read_bytes();assert len(payload)==expected['bytes']==count*16 and sha(p)==expected['sha256']
    native=[Path(k) for k in g['source_stream_pins'] if Path(k).name=='uv1.f32'];assert len(native)==1
    assert sha(native[0])==g['source_stream_pins'][str(native[0])]==expected['sha256']
    values=list(struct.iter_unpack('<4f',payload));assert all(math.isfinite(x) for v in values for x in v)
    pins[str(gpath)]=sha(gpath);pins[str(p)]=sha(p);pins[str(native[0])]=sha(native[0])
    geometries[str(base)]=dict(geometry_receipt=str(gpath),geometry_receipt_sha256=sha(gpath),source_uv1=str(p),native_uv1=str(native[0]),uv1_sha256=sha(p),vertices=count,source_dimension=format[0]['dimension'],source_y_min=min(v[1] for v in values),source_y_max=max(v[1] for v in values),shader_consumption_pending=True)
   rows.append(dict(asset_id=asset['asset_id'],node_path=binding['node_path'],mesh_guid=mesh['guid'],mesh_file_id=mesh['file_id'],geometry=str(base),material_guids=hits))
 assert len(rows)==52
 for p,h in pins.items():assert sha(p)==h
 ROOT.mkdir()
 result=dict(schema='hivemind.source-vector-uv1-reopen.v1',observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),renderer_nodes_checked=len(rows),unique_geometry_streams_checked=len(geometries),material_kernels=6,rows=rows,geometries=geometries,active_uv1_consumption=consumption,input_pins=pins,source_y_bytes_preserved=True,uv0_substitution_performed=False,renderer_installed=False,gpu_shader_fetch_qualified=False,render_parity_qualified=False,full_import=False)
 with (ROOT/'RESULT.v1.json').open('x',encoding='utf-8') as f:json.dump(result,f,indent=2)
 print(json.dumps(dict(renderer_nodes=len(rows),unique_geometries=len(geometries),receipt=str(ROOT/'RESULT.v1.json'))))
if __name__=='__main__':main()
