"""Keep the guarded Game scene; repair the generated cleanup comment delimiter."""
from pathlib import Path
BASE_V6=Path(__file__).with_name('queue_firepit_shadow_reference_v6.py')
source=BASE_V6.read_text('utf-8').replace('firepit-shadow-source-reference-v6','firepit-shadow-source-reference-v7').replace('SourceShadowReferenceV6','SourceShadowReferenceV7')
source=source.replace('// The guarded private unsaved scene is discarded when this batch process exits.','/* The guarded private unsaved scene is discarded when this batch process exits. */')
exec(compile(source,str(BASE_V6),'exec'),globals())
