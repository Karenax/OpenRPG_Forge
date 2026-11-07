"""Reopen current common-host LOD readbacks; retain raw data and fixed sRGB previews.

This verifies diagnostic draws and repetition only, never Unity fidelity or runtime LOD.
"""
from pathlib import Path
import datetime, hashlib, json
import numpy as np
from PIL import Image, ImageDraw

W=Path('F:/OpenRPG_ImportWork')
I=W/'firepit-current-lod-gpu-v220'
O=W/'firepit-current-lod-gpu-reopen-v221'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()

def main():
    state=read(I/'RUN-RESULT.v1.json')
    assert state['status']=='current_common_host_four_lod_eight_gpu_captures_retained',state
    assert not O.exists();O.mkdir()
    result={'status':'lod_gpu_reopen_in_progress','full_import':False}
    try:
        prep=read(I/'PREPARED.v1.json')
        for p,h in prep['input_pins'].items():assert sha(p)==h,p
        assert sha(I/'capture.exe')==state['exe_sha256']
        assert sha(I/'NATIVE-RESULT.v1.json')==state['native_sha256']
        native=read(I/'NATIVE-RESULT.v1.json');assert native['nodes']==9 and native['primitives']==4
        assert len(native['captures'])==8
        rows=[];previews=[];all_pins=dict(prep['input_pins'])
        for level in range(4):
            captures=[r for r in native['captures'] if r['lod']==level]
            assert len(captures)==2 and sorted(r['repeat'] for r in captures)==[0,1]
            raws=[]
            for r in captures:
                p=Path(r['raw_path']);assert p.parent==I and sha(p)==state['capture_pins'][str(p)]
                meta=read(Path(r['metadata_path']));assert meta['format']=='Rgba16Float' and meta['width']==meta['height']==512 and meta['bytes']==p.stat().st_size==2097152
                assert meta['gpu_readback'] and len(r['visible_nodes'])==1
                pixels=np.frombuffer(p.read_bytes(),dtype='<f2').reshape(512,512,4)
                assert np.isfinite(pixels).all(),'Nonfinite GPU pixels'
                raws.append(pixels);all_pins[str(p)]=sha(p);all_pins[r['metadata_path']]=sha(r['metadata_path'])
            assert captures[0]['visible_nodes']==captures[1]['visible_nodes']
            assert raws[0].tobytes()==raws[1].tobytes(),'Unstable repeat capture'
            rgb=raws[0][:,:,:3].astype(np.float32)
            background=rgb[0,0];changed=np.any(rgb!=background,axis=2);assert changed.any(),'Empty background-only frame'
            ys,xs=np.where(changed)
            pipeline=I/f'LOD-{level}-PIPELINES.json';pipelines=read(pipeline)
            assert pipelines and all(p['state']=='ready' and p['error'] is None for p in pipelines)
            all_pins[str(pipeline)]=sha(pipeline)
            clipped=np.clip(rgb,0,1)
            srgb=np.where(clipped<=0.0031308,clipped*12.92,1.055*np.power(clipped,1/2.4)-0.055)
            png=O/f'lod-{level}.png';Image.fromarray(np.rint(srgb*255).astype(np.uint8)).save(png)
            previews.append(png)
            rows.append(dict(lod=level,visible_node=captures[0]['visible_nodes'][0],threshold=captures[0]['threshold'],stable_byte_equal=True,non_background_pixels=int(changed.sum()),non_background_bbox=[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)],rgb_min=float(rgb.min()),rgb_max=float(rgb.max()),preview=str(png),preview_sha256=sha(png),source_pipelines=len(pipelines)))
        sheet=Image.new('RGB',(1024,1064),(25,25,25));draw=ImageDraw.Draw(sheet)
        for n,p in enumerate(previews):
            x=(n%2)*512;y=(n//2)*532;sheet.paste(Image.open(p),(x,y+20));draw.text((x+8,y+3),f'LOD {n} | common host | fixed linear to sRGB',(230,230,230))
        sheet.save(O/'CONTACT.png')
        all_pins.update({str(p):sha(p) for p in [Path(__file__),I/'RUN-RESULT.v1.json',I/'NATIVE-RESULT.v1.json',I/'capture.exe']})
        result.update(status='four_authored_lod_diagnostic_readbacks_independently_reopened',rows=rows,input_pins=all_pins,preview_transfer='clamp exposed linear HDR to [0,1], standard sRGB; no fitted correction',contact=str(O/'CONTACT.png'),contact_sha256=sha(O/'CONTACT.png'),visual_review_pending=True,automatic_lod_selection_verified=False,effects_rendered=False,source_lighting_parity=False,studio_verified=False)
    except Exception as error:
        result.update(status='lod_gpu_reopen_refusal_retained',error=repr(error))
        raise
    finally:
        result['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
        (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))

if __name__=='__main__':main()
