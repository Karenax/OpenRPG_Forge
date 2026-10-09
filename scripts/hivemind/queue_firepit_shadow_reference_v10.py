"""Invoke the environment exporter explicitly; preserve the wrong-method v9 trial."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_shadow_reference_v9.py')
loaded=runpy.run_path(str(BASE),run_name='environment_method_repair')
g=loaded['g'];original_helper=g['helper'];popen=g['subprocess'].Popen
def helper():return original_helper().replace('SourceShadowReferenceV9','SourceShadowReferenceV10')
def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export'
        args[i]='OpenRPGSourceShadowReferenceV10.Export'
    return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-shadow-source-reference-v10',HELPER=g['PROJECT']/'Assets/Editor/SourceShadowReferenceV10.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':g['prepare']()
    else:
        g['run']()
        state=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        missing=[str(g['ROOT']/f'SOURCE-STATE-{i}.json') for i in range(2) if not (g['ROOT']/f'SOURCE-STATE-{i}.json').exists()]
        if state.get('actual_exit_code')==0 and not missing:
            state.update(status='source_shadow_pair_and_effective_environment_captured',environment_files={str(g['ROOT']/f'SOURCE-STATE-{i}.json'):g['sha'](g['ROOT']/f'SOURCE-STATE-{i}.json') for i in range(2)})
        else:state.update(status='source_effective_environment_capture_refused',missing_environment_files=missing)
        g['save']('RUN-RESULT.v1.json',state)
