"""Version source reopen and native comparison for the depth-bound probe."""
from pathlib import Path
import subprocess, sys

s=Path(__file__).parent
sealer=s/'seal_townsmith_depth_bound_uv_v27.py'
comparison=s/'compare_townsmith_depth_bound_uv_v27.py'
if sealer.exists() or comparison.exists():
    archive=Path('F:/OpenRPG_ImportWork/townsmith-door-source-depth-bound-uv-v27/comparator-encoding-failure')
    assert not archive.exists()
    archive.mkdir()
    for p in [sealer,comparison]:
        if p.exists(): (archive/(p.name+'.saved')).write_bytes(p.read_bytes())
text=(s/'seal_townsmith_source_uv_v25.py').read_text(encoding='utf-8')
text=text.replace('townsmith-door-source-uv-derivatives-v25','townsmith-door-source-depth-bound-uv-v27')
text=text.replace('townsmith-door-source-uv-reopen-v25','townsmith-door-source-depth-bound-uv-reopen-v27')
text=text.replace("assert record['mode'] == index % 2", "assert record['actual_opaque_depth_bound'] and record['viewportWidth']==512 and record['viewportHeight']==512\n            assert record['mode'] == index % 2")
text=text.replace("reshape(512, 512, 4)", "reshape(record['height'],record['width'],4)[:512,:512]")
text=text.replace('Private original LOD0 submesh0 draw inside source camera;', 'Private original LOD0 submesh0 draw bound to actual opaque depth inside source camera;')
text=text.replace('L03.TOWNSMITH.MATERIAUX.UV.SOURCE','L03.TOWNSMITH.MATERIAUX.UV.SOURCE.PROFONDEUR')
text=text.replace('24 captures float32,','24 captures float32 attachées à la profondeur opaque réelle,')
compile(text,str(sealer),'exec');sealer.write_text(text,encoding='utf-8')
text=(s/'compare_townsmith_native_uv_v26.py').read_text(encoding='utf-8')
text=text.replace("root=w/'townsmith-native-uv-comparison-v26'", "root=w/'townsmith-depth-bound-uv-comparison-v27'")
text=text.replace('townsmith-door-source-uv-reopen-v25','townsmith-door-source-depth-bound-uv-reopen-v27')
text=text.replace('townsmith-door-source-uv-derivatives-v25','townsmith-door-source-depth-bound-uv-v27')
text=text.replace("uv=np.fromfile(uvpath,dtype='<f4').reshape(512,512,4)[::-1]", "record=read(source/'door-nopost-linear.uv-control-6.json')\n    assert record['actual_opaque_depth_bound']\n    uv=np.fromfile(uvpath,dtype='<f4').reshape(record['height'],record['width'],4)[:512,:512][::-1]")
text=text.replace("derivatives=np.fromfile(dpath,dtype='<f4').reshape(512,512,4)[::-1]", "derivatives=np.fromfile(dpath,dtype='<f4').reshape(record['height'],record['width'],4)[:512,:512][::-1]")
text=text.replace('Diagnostic of original isolated LOD0 submesh0;', 'Diagnostic of original isolated LOD0 submesh0 with actual source opaque depth selection;')
text=text.replace('townsmith-native-uv-comparison-v26/RESULT.v1.json','townsmith-depth-bound-uv-comparison-v27/RESULT.v1.json')
text=text.replace('L03.TOWNSMITH.MATERIAUX.UV.COMPARAISON','L03.TOWNSMITH.MATERIAUX.UV.COMPARAISON.PROFONDEUR')
compile(text,str(comparison),'exec');comparison.write_text(text,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(sealer)],check=True)
subprocess.run([sys.executable,'-B',str(comparison)],check=True)
