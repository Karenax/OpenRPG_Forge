"""Filter static renderers while retaining the base Renderer API used by the capture."""
from pathlib import Path
BASE_SCRIPT=Path(__file__).with_name('queue_firepit_shadow_reference_v1.py')
source=BASE_SCRIPT.read_text('utf-8').replace('firepit-shadow-source-reference-v1','firepit-shadow-source-reference-v3').replace('SourceShadowReferenceV1','SourceShadowReferenceV3')
needle=' return text\ndef prepare():'
assert source.count(needle)==1
source=source.replace(needle," text=replace(text,'source.GetComponentsInChildren<Renderer>(true).Select','source.GetComponentsInChildren<Renderer>(true).Where(r=>r is MeshRenderer).Select')\n return text\ndef prepare():")
source=source.replace("str(BASE):sha(BASE),","str(BASE):sha(BASE),str(BASE_SCRIPT):sha(BASE_SCRIPT),")
exec(compile(source,str(BASE_SCRIPT),'exec'),globals())
