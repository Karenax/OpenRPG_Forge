"""Identify source default RG through unclamped native surface response."""
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
ROOT = Path('F:/OpenRPG_ImportWork/detail-default-native-gain-v11')

def configure():
    text = (HERE/'queue_source_null_mask_surface_v10.py').read_text('utf-8-sig')
    text = text.replace('unity-source-null-mask-surface-v10','detail-default-native-gain-v11')
    text = text.replace('SurfaceV10','GainV11')
    scope = dict(__file__=str(Path(__file__).resolve()),__name__='configured_gain_protocol')
    exec(compile(text,str(Path(__file__).resolve()),'exec'),scope)
    ns = scope['configure']()
    return ns

if __name__=='__main__':
    ns = configure()
    if len(sys.argv)>1 and sys.argv[1]=='run':
        ns['run']()
    else:
        assert not ROOT.exists()
        previous = ns['read'](ROOT.parent/'unity-source-null-mask-surface-v10/RUN-RESULT.v1.json')
        assert previous['unity_exit_code']==0 and previous['finished_utc']
        helper = HERE/'candidates/SourceNullMaskHdrpGainV11Probe.cs'
        assert not helper.exists()
        text = (HERE/'candidates/SourceNullMaskHdrpSurfaceV10Probe.cs').read_text('utf-8-sig')
        text = text.replace('SurfaceV10','GainV11')
        replacements = [
            ('public float raw_hd_camera_time,effective_hd_camera_time;',
             'public float diagnostic_clone_roughness,diagnostic_clone_ao_intensity,raw_hd_camera_time,effective_hd_camera_time;'),
            ('var clone=new Material(material); clone.hideFlags=HideFlags.HideAndDontSave;',
             '''var clone=new Material(material); clone.hideFlags=HideFlags.HideAndDontSave;
      if(!clone.HasProperty("_Roughness")||!clone.HasProperty("_AOIntensity"))throw new Exception("Original gain properties absent");
      // Temporary diagnostic clone only. Author material remains byte-exact.
      clone.SetFloat("_Roughness",0.125f);clone.SetFloat("_AOIntensity",0.25f);
      if(clone.GetFloat("_Roughness")!=0.125f||clone.GetFloat("_AOIntensity")!=0.25f)throw new Exception("Diagnostic gain refused");'''),
            ('rows.Add(new Row{debug_surface_mode=',
             'rows.Add(new Row{diagnostic_clone_roughness=clone.GetFloat("_Roughness"),diagnostic_clone_ao_intensity=clone.GetFloat("_AOIntensity"),debug_surface_mode=')]
        for old,new in replacements:
            assert text.count(old)==1,old
            text=text.replace(old,new)
        helper.write_text(text,'utf-8')
        ns['HELPER']=helper
        ns['prepare']()
