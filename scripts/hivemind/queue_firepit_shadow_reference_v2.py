"""Keep particle effects in L06; this source reference covers four static LOD meshes."""
from pathlib import Path
BASE_SCRIPT=Path(__file__).with_name('queue_firepit_shadow_reference_v1.py')
source=BASE_SCRIPT.read_text('utf-8').replace('firepit-shadow-source-reference-v1','firepit-shadow-source-reference-v2').replace('SourceShadowReferenceV1','SourceShadowReferenceV2')
needle=' return text\ndef prepare():'
assert source.count(needle)==1
source=source.replace(needle," text=replace(text,'source.GetComponentsInChildren<Renderer>(true).Select','source.GetComponentsInChildren<MeshRenderer>(true).Select')\n return text\ndef prepare():")
source=source.replace("str(BASE):sha(BASE),","str(BASE):sha(BASE),str(BASE_SCRIPT):sha(BASE_SCRIPT),")
exec(compile(source,str(BASE_SCRIPT),'exec'),globals())
