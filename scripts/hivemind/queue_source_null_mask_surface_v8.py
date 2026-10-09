"""Native HDRP surface-data probe: original Occlusion and Smoothness sinks.

This is not lit-scene or original-prefab qualification. Keep exact repeat and
white/black controls, retain all warmups, and restore the native debug object.
"""
from pathlib import Path

HERE = Path(__file__).resolve().parent

def once(text, old, new):
    assert text.count(old) == 1, old
    return text.replace(old, new)

def surface_helper(text):
    text = once(text, 'public int camera,width=32,height=32;',
        'public int camera,debug_surface_mode,width=32,height=32; public bool native_debug_mode_active;')
    text = once(text, 'public bool source_material_unchanged,',
        'public bool debug_material_settings_restored,surface_data_only=true; public bool source_material_unchanged,')
    text = once(text, 'var rows=new System.Collections.Generic.List<Row>();',
        '''var nativePipeline=RenderPipelineManager.currentPipeline as HDRenderPipeline;
  if(nativePipeline==null)throw new Exception("Native HDRP pipeline missing");
  var debug=nativePipeline.debugDisplaySettings;
  var originalMaterialDebug=debug.data.materialDebugSettings;
  var currentDebug=typeof(HDRenderPipeline).GetField("m_CurrentDebugDisplaySettings",BindingFlags.Instance|BindingFlags.NonPublic);
  if(currentDebug==null)throw new Exception("Actual HDRP current debug state missing");
  var diagnosticMaterialDebug=new MaterialDebugSettings();
  var rows=new System.Collections.Generic.List<Row>();''')
    text = once(text, 'for(int view=0;view<directions.Length;view++) {',
        '''for(int view=0;view<directions.Length*2;view++) {
    int mode=view<directions.Length?1005:1006;
    debug.data.materialDebugSettings=diagnosticMaterialDebug;
    diagnosticMaterialDebug.SetDebugViewMaterial(mode);''')
    text = text.replace('directions[view]', 'directions[view%directions.Length]')
    text = once(text, 'var afterCamera=(HDCamera)getCamera.Invoke',
        '''var activeDebug=(DebugDisplaySettings)currentDebug.GetValue(nativePipeline);
      if(!ReferenceEquals(activeDebug,debug)||activeDebug.data.materialDebugSettings.debugViewMaterial[0]!=1||activeDebug.data.materialDebugSettings.debugViewMaterial[1]!=mode)
       throw new Exception("Requested native surface debug mode was not active");
      var afterCamera=(HDCamera)getCamera.Invoke''')
    text = once(text, 'rows.Add(new Row{raw_hd_camera_time=',
        'rows.Add(new Row{debug_surface_mode=mode,native_debug_mode_active=true,raw_hd_camera_time=')
    text = once(text, 'var report=new Report{unity_version=',
        '''debug.data.materialDebugSettings=originalMaterialDebug;
   if(!ReferenceEquals(debug.data.materialDebugSettings,originalMaterialDebug))throw new Exception("Native material debug restoration refused");
   var report=new Report{debug_material_settings_restored=true,unity_version=''')
    text = once(text, '}finally {target.Release();',
        '}finally {debug.data.materialDebugSettings=originalMaterialDebug;target.Release();')
    return text

def main():
    text = (HERE / 'queue_source_null_mask_hdrp_v7.py').read_text('utf-8-sig')
    text = text.replace('unity-source-null-mask-hdrp-v7', 'unity-source-null-mask-surface-v8')
    text = text.replace('WarmV7', 'SurfaceV8')
    text = text.replace("unity-source-null-mask-hdrp-v6/RUN-RESULT.v1.json", "unity-source-null-mask-hdrp-v7/RUN-RESULT.v1.json")
    text = text.replace('expected_images=18', 'expected_images=36')
    text = text.replace("len(report['rows']) == 18", "len(report['rows']) == 36")
    text = text.replace('pixels_reopened=18432', 'pixels_reopened=36864')
    text = text.replace('images_verified=18', 'images_verified=36').replace('pixels_verified=18432', 'pixels_verified=36864')
    text = text.replace("for row in report['rows']) == 6", "for row in report['rows']) == 12")
    text = text.replace('warmup_images_retained=6,comparison_images=12', 'warmup_images_retained=12,comparison_images=24')
    text = once(text, "text=text.replace('pixels_reopened=12288','pixels_reopened=36864')",
        "text=text.replace('pixels_reopened=12288','pixels_reopened=36864')\n            text=text.replace('assert set(groups) == {0, 1, 2}', 'assert set(groups) == set(range(6))')\n            text=text.replace(\"assert report['diagnostic_preview_scene_isolated']\", \"assert report['surface_data_only'] and report['debug_material_settings_restored']\\n        assert all(row['native_debug_mode_active'] and row['debug_surface_mode'] == (1005 if row['camera'] < 3 else 1006) for row in report['rows'])\\n        assert report['diagnostic_preview_scene_isolated']\")")
    text = once(text, 'cold_start_qualified=False,autonomous_behavior_qualified=False)',
        'cold_start_qualified=False,autonomous_behavior_qualified=False,lit_render_qualified=False,surface_data_only=True)')
    text = once(text, "for path in [BASE,sdk/'Camera/HDCamera.cs',sdk/'HDRenderPipeline.cs']:",
        "for path in [BASE,sdk/'Camera/HDCamera.cs',sdk/'HDRenderPipeline.cs',sdk/'HDRenderPipeline.Debug.cs',sdk.parent/'Debug/DebugDisplay.cs',sdk.parent/'Debug/MaterialDebug.cs',sdk.parent/'Material/Lit/Lit.cs.hlsl']:")
    text = once(text, "helper.write_text(text,'utf-8');", "text=surface_helper(text)\n        helper.write_text(text,'utf-8');")
    namespace = dict(__file__=str(Path(__file__).resolve()), __name__='__main__', surface_helper=surface_helper)
    exec(compile(text, str(Path(__file__).resolve()), 'exec'), namespace)

if __name__ == '__main__':
    main()
