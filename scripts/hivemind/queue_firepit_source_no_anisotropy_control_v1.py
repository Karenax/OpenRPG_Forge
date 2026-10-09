"""Private quality control: preserve main references, then disable forced anisotropy."""
from pathlib import Path
import runpy
import sys

BASE=Path(__file__).with_name('queue_firepit_source_surface_debug_v1.py')
loaded=runpy.run_path(str(BASE),run_name='no_anisotropy_surface_probe')
g=loaded['g'];original_helper=g['helper'];popen=loaded['popen']
ROLES=loaded['ROLES']

def helper():
    text=original_helper().replace('SourceSurfaceDebugV1','SourceNoAnisotropyControlV1')
    debug=loaded['DEBUG'];assert text.count(debug)==1
    prefix=r'''
   var previousAnisotropy=QualitySettings.anisotropicFiltering;
   File.WriteAllText(Path.Combine(Path.GetDirectoryName(request.output),"QUALITY-STATE.json"),"{\"quality_level\":"+QualitySettings.GetQualityLevel()+",\"quality_name\":\""+QualitySettings.names[QualitySettings.GetQualityLevel()]+"\",\"anisotropic_filtering\":\""+previousAnisotropy+"\",\"control\":\"Disable\",\"original_materials_used\":true}");
   try {QualitySettings.anisotropicFiltering=AnisotropicFiltering.Disable;
'''
    return text.replace(debug,prefix+debug+'\n }finally{QualitySettings.anisotropicFiltering=previousAnisotropy;}\n')

def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export'
        args[i]='OpenRPGSourceNoAnisotropyControlV1.Export'
    return popen(args,*a,**kw)

g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-no-anisotropy-control-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceNoAnisotropyControlV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':
        g['prepare']()
    else:
        g['run']();d=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        files=[g['ROOT']/f'DEBUG-{role}.rgba16f' for role in ROLES]
        if d.get('actual_exit_code')==0 and all(p.exists() for p in files):
            d.update(status='original_source_surface_debug_with_private_anisotropy_disabled_captured',debug_files={str(p):g['sha'](p) for p in files},quality_state=g['read'](g['ROOT']/'QUALITY-STATE.json'))
        else:
            d.update(status='source_no_anisotropy_control_capture_refused')
        g['save']('RUN-RESULT.v1.json',d)
