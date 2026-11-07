"""CPU source-PNG samples at measured UVs; not a GPU rendering claim."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
import numpy as np
from PIL import Image
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-uv-albedo-measurements-v933';P=W/'mountain-hdrp-capture-stage-v869/project'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def samples(pixels,uvs,mode):
    coords=np.asarray([[v['x'],v['y']] for v in uvs],dtype=np.float64);assert np.isfinite(coords).all()
    h,w,_=pixels.shape
    if mode=='repeat':coords=coords%1.0
    else:coords=np.clip(coords,0,1)
    xy=coords*np.array([w,h])-.5;x0=np.floor(xy[:,0]).astype(int);y0=np.floor(xy[:,1]).astype(int);fraction=xy-np.column_stack([x0,y0])
    def index(v,limit):return v%limit if mode=='repeat' else np.clip(v,0,limit-1)
    x1=index(x0+1,w);y1=index(y0+1,h);x0=index(x0,w);y0=index(y0,h)
    # PNG top-down rows; Unity UV origin is bottom-left.
    a=pixels[h-1-y0,x0];b=pixels[h-1-y0,x1];c=pixels[h-1-y1,x0];d=pixels[h-1-y1,x1]
    u,v=fraction[:,0,None],fraction[:,1,None];values=(a*(1-u)+b*u)*(1-v)+(c*(1-u)+d*u)*v
    return dict(samples=len(values),uv_min=coords.min(axis=0).tolist(),uv_max=coords.max(axis=0).tolist(),mean_rgba=values.mean(axis=0).tolist(),dark_rgb_fraction=float(np.mean(np.max(values[:,:3],axis=1)<=1/255)),transparent_fraction=float(np.mean(values[:,3]<=1/255)))
def main():
    rp=W/'mountain-native-uv-normal-diagnostic-v932/RUN-RESULT.v1.json';run=read(rp)
    assert run['status']=='native_speedtree_uv_normal_tangent_streams_measured' and run['unity_exit_code']==0 and not run['input_changes']
    npth=rp.parent/'NATIVE-RESULT.v1.json';assert sha(npth)==run['native_result_sha256'];native=read(npth)
    pins={str(p):sha(p) for p in [rp,npth,Path(__file__).resolve()]}
    for p,d in run['input_pins'].items():assert sha(p)==d,p;pins[p]=d
    rows=[]
    for card in native['cards']:
        assert card['authored_objects_unchanged']
        for renderer in card['renderers']:
            for slot in renderer['slots']:
                if 'SpeedTree' not in slot['material']['shader']:continue
                texture=next(p for p in slot['material']['properties'] if p['name']=='_MainTex');path=P/texture['texture_path'];assert str(path) in pins and sha(path)==pins[str(path)]
                with Image.open(path) as image:pixels=np.array(image.convert('RGBA'),dtype=np.float64)/255
                measurements={mode:{kind:samples(pixels,slot[key],mode) for kind,key in [('unique_vertices','vertex_uv0'),('triangle_centroids','triangle_centroid_uv0')]} for mode in ['repeat','clamp']}
                rows.append(dict(id=card['id'],submesh=slot['submesh'],main_texture=texture['texture_path'],normals=dict(count=slot['normal_count'],zero=slot['zero_normals'],min_length=slot['normal_length_min'],max_length=slot['normal_length_max']),tangents=dict(count=slot['tangent_count'],zero=slot['zero_tangents'],min_length=slot['tangent_length_min'],max_length=slot['tangent_length_max']),uv_channel_counts=slot['uv_channel_counts'],source_png_uv0_samples=measurements))
    assert len(rows)==3
    O.mkdir(exist_ok=False);result=dict(status='held_tree_uv0_source_albedo_samples_and_attribute_lengths_measured',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,input_pins=pins,scope='Finite source-PNG RGBA bilinear samples at native UV0 vertices and triangle centroids, repeat and clamp evaluated separately. No shader evaluation, lighting, raster coverage, mip or GPU parity claim.',root_cause_qualified=False,source_mutations=0,full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(status=result['status'],rows=[dict(id=r['id'],vertex_dark_repeat=r['source_png_uv0_samples']['repeat']['unique_vertices']['dark_rgb_fraction'],centroid_dark_repeat=r['source_png_uv0_samples']['repeat']['triangle_centroids']['dark_rgb_fraction'],centroid_dark_clamp=r['source_png_uv0_samples']['clamp']['triangle_centroids']['dark_rgb_fraction'],zero_normals=r['normals']['zero'],zero_tangents=r['tangents']['zero']) for r in rows])))
if __name__=='__main__':main()
