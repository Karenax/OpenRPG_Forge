"""Compare native output to an explicit private Unity quality control."""
from pathlib import Path
import runpy
import json
import hashlib

BASE=Path(__file__).with_name('reopen_firepit_surface_packed_hdr_control_v1.py')
g=runpy.run_path(str(BASE),run_name='anisotropy_quality_comparison')
CONTROL=g['W']/'firepit-source-no-anisotropy-control-v1'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()

g.update(read=read,sha=sha)

def main():
    run=g['read'](CONTROL/'RUN-RESULT.v1.json');prep=g['read'](CONTROL/'PREPARED.v1.json')
    assert run['actual_exit_code']==0 and run['source_inputs_unchanged'] and not run['private_changed']
    assert len(prep['dependency_paths'])==24
    assert run['quality_state']['anisotropic_filtering']=='ForceEnable'
    for path,expected in prep['input_pins'].items():
        assert g['sha'](path)==expected,path
    for view in range(2):
        for rel in [f'images/{view}.png',f'linear-{view}.rgba16f']:
            assert g['sha'](CONTROL/rel)==g['sha'](g['W']/'firepit-shadow-source-reference-v8'/rel)
    source=g['source'].replace('firepit-surface-packed-hdr-control-reopen-v1','firepit-no-anisotropy-source-control-comparison-v1')
    needle="refs=protocol['source_surface_debug_refs'];assert len(refs)==5"
    assert source.count(needle)==1
    replacement="refs=[dict(r,path=str(SOURCE_CONTROL/f\"DEBUG-{r['role']}.rgba16f\"),sha256=SOURCE_CONTROL_RUN['debug_files'][str(SOURCE_CONTROL/f\"DEBUG-{r['role']}.rgba16f\")]) for r in protocol['source_surface_debug_refs']];assert len(refs)==5"
    source=source.replace(needle,replacement)
    source=source.replace('source_debug_encoding_applied=True,','source_debug_encoding_applied=True,private_source_quality_override=True,original_source_quality_force_enable=True,source_control_quality_disable=True,source_original_render_fidelity_qualified=False,')
    source=source.replace('files.extend([raw,meta,',"files.extend([SOURCE_CONTROL/'RUN-RESULT.v1.json',SOURCE_CONTROL/'PREPARED.v1.json',SOURCE_CONTROL/'QUALITY-STATE.json',SOURCE_CONTROL_HELPER,raw,meta,")
    g.update(SOURCE_CONTROL=CONTROL,SOURCE_CONTROL_RUN=run,SOURCE_CONTROL_HELPER=BASE,__file__=str(Path(__file__).resolve()),__name__='__main__')
    exec(compile(source,str(BASE),'exec'),g)

if __name__=='__main__':main()
