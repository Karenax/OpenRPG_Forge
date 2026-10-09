"""Diagnostic presentation only; preserve raw buffers and pixel protocol."""
from pathlib import Path
import hashlib,json,struct
from PIL import Image,ImageDraw
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'firepit-surface-layout-review-v1'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    target=W/'firepit-surface-native-v1/gpu-0.rgba16f'
    raw=list(struct.iter_unpack('<4e',target.read_bytes()))
    roles=['Albedo','Normal','Smoothness','AmbientOcclusion','Metal']
    canvas=Image.new('RGB',(1024,5*280),(24,24,24));draw=ImageDraw.Draw(canvas)
    pins={str(target):sha(target)}
    for mode,role in enumerate(roles):
        source=W/f'firepit-source-surface-debug-v1/DEBUG-{role}.rgba16f'
        pixels=list(struct.iter_unpack('<4e',source.read_bytes()))
        pins[str(source)]=sha(source)
        views=[]
        for native in [False,True]:
            im=Image.new('RGB',(512,240))
            data=[]
            for y in range(140,380):
                for x in range(512):
                    i=y*512+x
                    # Nearest role sample is for display only, never quantitative alignment.
                    j=i+(mode-i%5)%5
                    if j//512!=y:j-=5
                    p=raw[j] if native else pixels[(511-y)*512+x]
                    data.append(tuple(round(255*min(1,max(0,v))) for v in p[:3]))
            im.putdata(data);views.append(im)
        for col,im in enumerate(views):canvas.paste(im,(col*512,mode*280+30))
        draw.text((8,mode*280+8),role+' Unity raw / Forge nearest diagnostic role',fill='white')
    ROOT.mkdir();canvas.save(ROOT/'surface-layout.png')
    p=W/'firepit-surface-native-case-v1/private-surface-diagnostic.glb'
    b=p.read_bytes();n=struct.unpack_from('<I',b,12)[0];d=json.loads(b[20:20+n]);binary=b[28+n:]
    item=d['materials'][0]['extensions']['OPENRPG_source_graph']['forward']
    v=d['bufferViews'][item['buffer_view']]
    s=binary[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']].decode()
    (ROOT/'forward-inspection.wgsl').write_text(s,'utf-8');pins[str(p)]=sha(p)
    result=dict(status='diagnostic_surface_layout_presentation_created',input_pins=pins,nearest_role_for_display_only=True,raw_comparison_protocol_unchanged=True,full_import=False,editor_visual_verified=False)
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(result))
if __name__=='__main__':main()
