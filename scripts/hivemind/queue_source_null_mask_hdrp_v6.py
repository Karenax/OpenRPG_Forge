"""Pin the actual HDRP StandardRequest camera clock, not global-vector guesses."""
from pathlib import Path
import ast
import importlib.util
import json
import sys

HERE=Path(__file__).resolve().parent
BASE=HERE/'queue_source_null_mask_hdrp_v5.py'
spec=importlib.util.spec_from_file_location('isolated_mask_v5',BASE)
probe=importlib.util.module_from_spec(spec);spec.loader.exec_module(probe)
ROOT=Path('F:/OpenRPG_ImportWork/unity-source-null-mask-hdrp-v6')
probe.ROOT=ROOT;probe.probe.ROOT=ROOT


def configure():
    namespace=probe.configure();namespace['__file__']=str(Path(__file__).resolve())
    original=probe.probe.probe.BASE.read_text('utf-8-sig')
    for name in ('prepare','run'):
        node=next(n for n in ast.parse(original).body if isinstance(n,ast.FunctionDef) and n.name==name)
        text=ast.unparse(node).replace('OpenRPGSourceNullMaskHdrpProbe.Export','OpenRPGSourceNullMaskHdrpClockV6Probe.Export')
        text=text.replace('SourceNullMaskHdrpProbe.cs','SourceNullMaskHdrpClockV6Probe.cs')
        if name=='prepare':
            text=probe.once(text,"assert sha(target) == digest, 'Existing private source differs; retain it: ' + str(target)",
                'if sha(target) != digest: digest = retain_private_context(origin, target, digest)')
        else:
            text=probe.once(text,"report = read(ROOT / 'NATIVE.v1.json')",
                "report = read(ROOT / 'NATIVE.v1.json')\n        assert report['diagnostic_preview_scene_isolated'] and report['active_editor_scene_preserved']\n        assert report['diagnostic_camera_postprocess_disabled'] and report['diagnostic_camera_exposure_disabled']\n        assert all(row['effective_hd_camera_time'] == 0 and row['same_hd_request_camera'] for row in report['rows'])")
        exec(compile(text,str(Path(__file__).resolve()),'exec'),namespace)
    return namespace


if __name__=='__main__':
    namespace=configure()
    if len(sys.argv)>1 and sys.argv[1]=='run':namespace['run']()
    else:
        assert not ROOT.exists()
        prior=json.loads((ROOT.parent/'unity-source-null-mask-hdrp-v5/RUN-RESULT.v1.json').read_text('utf-8-sig'))
        assert prior.get('finished_utc') and prior['unity_exit_code']==0
        assert prior['error']=='AssertionError: Native original render not repeatable'
        helper=HERE/'candidates/SourceNullMaskHdrpClockV6Probe.cs';assert not helper.exists()
        text=(HERE/'candidates/SourceNullMaskHdrpPreviewV5Probe.cs').read_text('utf-8-sig')
        text=text.replace('OpenRPGSourceNullMaskHdrpPreviewV5Probe','OpenRPGSourceNullMaskHdrpClockV6Probe')
        text=probe.once(text,'public string variant,path,sha256;',
            'public float raw_hd_camera_time,effective_hd_camera_time;public bool same_hd_request_camera;public string variant,path,sha256;')
        text=probe.once(text,'var rows=new System.Collections.Generic.List<Row>();', '''
  // StandardRequest selects CustomUserHistory0 in the installed HDRP SDK.
  var historyType=typeof(HDCamera).GetNestedType("HistoryChannel",BindingFlags.NonPublic|BindingFlags.Public);
  if(historyType==null)throw new Exception("Native HDRP history channel missing");
  var history=Enum.Parse(historyType,"CustomUserHistory0");
  var getCamera=typeof(HDCamera).GetMethod("GetOrCreate",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(Camera),typeof(int),historyType},null);
  var clock=typeof(HDCamera).GetProperty("animateMaterialsTime",BindingFlags.Instance|BindingFlags.NonPublic);
  var previousClock=typeof(HDCamera).GetProperty("animateMaterialsTimeLast",BindingFlags.Instance|BindingFlags.NonPublic);
  var animated=typeof(HDCamera).GetField("animateMaterials",BindingFlags.Instance|BindingFlags.NonPublic);
  if(getCamera==null||clock==null||previousClock==null||animated==null)throw new Exception("Native camera clock control missing");
  var hdCamera=(HDCamera)getCamera.Invoke(null,new object[]{camera,0,history});
  clock.SetValue(hdCamera,0f);previousClock.SetValue(hdCamera,0f);
  var rows=new System.Collections.Generic.List<Row>();''')
        text=probe.once(text,'rows.Add(new Row{variant=variant,', '''
      var afterCamera=(HDCamera)getCamera.Invoke(null,new object[]{camera,0,history});
      if(!ReferenceEquals(hdCamera,afterCamera))throw new Exception("Native request camera replaced");
      float effectiveTime=(bool)animated.GetValue(afterCamera)?(float)clock.GetValue(afterCamera):afterCamera.time;
      if(effectiveTime!=0)throw new Exception("Native request clock not fixed");
      rows.Add(new Row{raw_hd_camera_time=afterCamera.time,effective_hd_camera_time=effectiveTime,same_hd_request_camera=true,variant=variant,''')
        helper.write_text(text,'utf-8');namespace['HELPER']=helper
        namespace['prepare']()
