"""Reopen camera observations and unchanged draws; measure CPU projection only."""
from pathlib import Path
from datetime import datetime,timezone
import json,struct,math,hashlib
W=Path('F:/OpenRPG_ImportWork');CAP=W/'firepit-surface-camera-native-v1';ROOT=W/'firepit-surface-camera-native-reopen-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def cm(m,p):return [sum(m[j*4+i]*p[j] for j in range(4)) for i in range(4)]
def rm(m,p):return [sum(m[f'e{i}{j}']*p[j] for j in range(4)) for i in range(4)]
def pixel(p):return [(p[0]/p[3]+1)*256,(1-p[1]/p[3])*256]
def main():
    assert not ROOT.exists()
    run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');assert run['actual_exit_code']==0
    for p,h in prep['input_pins'].items():assert sha(p)==h,p
    fixture=Path(prep['fixture']);b=fixture.read_bytes();n=struct.unpack_from('<I',b,12)[0];d=json.loads(b[20:20+n]);raw=b[28+n:]
    node=read(CAP/'CASE.v1.json')['selected_source_node'];primitive=d['meshes'][d['nodes'][node]['mesh']]['primitives'][0]
    a=d['accessors'][primitive['attributes']['POSITION']];bv=d['bufferViews'][a['bufferView']];assert a['componentType']==5126 and a['type']=='VEC3'
    offset=bv.get('byteOffset',0)+a.get('byteOffset',0);stride=bv.get('byteStride',12)
    points=[list(struct.unpack_from('<3f',raw,offset+i*stride))+[1] for i in range(a['count'])]
    files=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',fixture];rows=[]
    for view in range(2):
        p=CAP/f'CAMERA-STATE-{view}.json';native=read(p);srcp=W/f'firepit-source-camera-matrices-v1/SOURCE-STATE-{view}.json';source=json.loads(read(srcp)['camera_matrices_json'])
        assert not native['gpu_uniform_readback'] and len(native['draw_transforms'])==1
        assert all(all(math.isfinite(v) for v in native[k]) for k in ['camera_world','view_from_world','clip_from_view','root_world','selected_node_world'])
        projected=[];errors=[]
        for point in points:
            world=cm(native['draw_transforms'][0]['world_from_local'],point)
            target=pixel(cm(native['clip_from_view'],cm(native['view_from_world'],world)))
            source_point=cm(native['source_uniform_rows'][0]['source_world_from_target_world'],world)
            original=pixel(rm(source['projection'],rm(source['world_to_camera'],source_point)))
            projected.append(target);errors.append(math.dist(target,original))
        gpu=CAP/f'gpu-{view}.rgba16f';previous=W/f'firepit-surface-native-v1/gpu-{view}.rgba16f';assert sha(gpu)==sha(previous)
        mask=[i for i,v in enumerate(struct.iter_unpack('<4e',gpu.read_bytes())) if v[3]==.5];assert mask
        rows.append(dict(view=view,projected_vertices=len(points),maximum_cpu_projection_distance_pixels=max(errors),mean_cpu_projection_distance_pixels=sum(errors)/len(errors),expected_unoccluded_vertex_envelope=[min(p[k] for p in projected) for k in range(2)]+[max(p[k] for p in projected) for k in range(2)],actual_gpu_model_mask_bounds=[min(i%512 for i in mask),min(i//512 for i in mask),max(i%512 for i in mask),max(i//512 for i in mask)],actual_gpu_model_pixels=len(mask),original_gpu_image_byte_identical=True,extracted_views_observed=len(native['extracted_views'])))
        files.extend([p,srcp,gpu,previous])
    result=dict(status='native_camera_observations_and_unchanged_surface_images_verified',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,source_pins_verified=len(prep['input_pins']),cpu_projection_only=True,gpu_uniform_readback=False,occluded_vertex_envelope_not_a_silhouette=True,pixel_alignment_fitted=False,render_fidelity_qualified=False,full_import=False,editor_visual_verified=False,input_pins={str(p):sha(p) for p in files},next_action='Inspect triangle orientation, culling and vertex attribute admission; use private controls and keep source geometry/material state unchanged.')
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
