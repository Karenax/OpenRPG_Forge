"""Supersede the render-loop-camera snapshot with actual request-history evidence."""
from pathlib import Path
BASE=Path(__file__).with_name('audit_firepit_source_environment_v1.py')
source=BASE.read_text('utf-8').replace('firepit-shadow-source-reference-v10','firepit-shadow-source-reference-v11').replace('firepit-shadow-effective-environment-v1','firepit-shadow-effective-environment-v2').replace('source_shadow_pair_and_effective_environment_captured','actual_request_history_camera_and_hdrp_probe_captured').replace('effective_unity_camera_environment_reopened','actual_request_history_environment_and_gpu_diffuse_probe_reopened')
source=source.replace("summary=dict(fog_enabled=", "assert d['source_history_channel']=='CustomUserHistory0' and len(d['hdrp_diffuse_probe_buffer'])==7\n    assert any(v!=0 for row in d['hdrp_diffuse_probe_buffer'] for v in row.values())\n    summary=dict(source_history_channel=d['source_history_channel'],hdrp_diffuse_probe_buffer=d['hdrp_diffuse_probe_buffer'],hdrp_cpu_probe_all_zero=all(v==0 for v in d['hdrp_ambient_probe']),fog_enabled=")
needle="files=[Path(__file__).resolve(),CAP/'RUN-RESULT.v1.json'"
sdk=Path('F:/OpenRPG_ImportWork/unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Runtime')
paths=[BASE,sdk/'RenderPipeline/Camera/HDCamera.cs',sdk/'RenderPipeline/HDRenderPipeline.cs',sdk/'Sky/SkyManager.cs',sdk/'Material/BuiltinGIUtilities.hlsl']
assert source.count(needle)==1
source=source.replace(needle,"files=[Path(__file__).resolve(),"+''.join('Path('+repr(str(p))+'),' for p in paths)+"CAP/'RUN-RESULT.v1.json'")
source=source.replace("retained_insufficient_trial=", "superseded_wrong_history_camera_audit=str(W/'firepit-shadow-effective-environment-v1/RESULT.v1.json'),retained_insufficient_trial=")
source=source.replace('Resolve source/target normal-lighting and shadow filter/cascade/bias discrepancies; no arbitrary ambient fill or fitted thresholds','Integrate measured HDRP diffuse SH and HDRI environment with explicit source basis and exposure, then repeat the original shadow comparison; preserve shadow filter/cascade/bias qualification')
exec(compile(source,str(BASE),'exec'),globals())
