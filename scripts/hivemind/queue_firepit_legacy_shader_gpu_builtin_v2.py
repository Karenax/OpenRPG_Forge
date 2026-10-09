"""Measure the original legacy shader under the supported Built-in API context."""
from pathlib import Path
import ast
import importlib.util
import sys

HERE = Path(__file__).resolve().parent
BASE = HERE / 'queue_firepit_legacy_shader_gpu_v1.py'
spec = importlib.util.spec_from_file_location('legacy_gpu_builtin', BASE)
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)
OLD = probe.ROOT
probe.ROOT = probe.W / 'unity-delta-firepit-legacy-shader-gpu-builtin-v2'
probe.job.ROOT = probe.ROOT
HELPER = HERE / 'candidates/LegacyParticleShaderGpuProbeBuiltin.cs'

def replace_once(text, old, new):
    assert text.count(old) == 1, 'source helper anchor differs'
    return text.replace(old, new)

def helper_source():
    text = probe.HELPER.read_text('utf-8-sig')
    text = text.replace('OpenRPGLegacyParticleShaderGpuProbe', 'OpenRPGLegacyParticleShaderGpuProbeBuiltin')
    text = replace_once(text, 'public string unity_version,color_space,gpu_name,gpu_type;',
        'public string unity_version,color_space,gpu_name,gpu_type,source_render_pipeline,measurement_render_pipeline;public bool pipeline_settings_restored;')
    text = replace_once(text, 'var originalFog=RenderSettings.fog;',
        'var originalGraphics=GraphicsSettings.defaultRenderPipeline;var originalQuality=QualitySettings.renderPipeline;Report capturedReport=null;var originalFog=RenderSettings.fog;')
    text = replace_once(text, 'RenderSettings.fog=false;',
        'GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;if(GraphicsSettings.currentRenderPipeline!=null)throw new Exception("Supported Built-in measurement context unavailable");RenderSettings.fog=false;')
    text = replace_once(text, 'File.WriteAllText(manifest.output,JsonUtility.ToJson(new Report{',
        'capturedReport=new Report{source_render_pipeline=(originalQuality!=null?originalQuality:originalGraphics)?.GetType().FullName??"Builtin",measurement_render_pipeline="Builtin",')
    text = replace_once(text, 'source_shader_fixture_gpu_captured=true},true));',
        'source_shader_fixture_gpu_captured=true};')
    text = replace_once(text, 'RenderTexture.active=previous;RenderSettings.fog=originalFog;',
        'GraphicsSettings.defaultRenderPipeline=originalGraphics;QualitySettings.renderPipeline=originalQuality;RenderTexture.active=previous;RenderSettings.fog=originalFog;')
    anchor = '  }\n }\n}\n'
    text = replace_once(text, anchor, '''  }
  if(GraphicsSettings.defaultRenderPipeline!=originalGraphics||QualitySettings.renderPipeline!=originalQuality)throw new Exception("Measurement pipeline restoration differs");
  if(capturedReport==null)throw new Exception("Original GPU report absent");capturedReport.pipeline_settings_restored=true;File.WriteAllText(manifest.output,JsonUtility.ToJson(capturedReport,true));
 }
}
''')
    return text

def verify(report):
    assert report['measurement_render_pipeline'] == 'Builtin'
    assert report['pipeline_settings_restored']
    return probe.verify(report)

def main():
    prior_path = OLD / 'RUN-RESULT.v1.json'
    prior = probe.job.read(prior_path)
    assert prior['unity_exit_code'] == 0 and prior['native_shader_fixture_gpu_verified']
    receipt = OLD / 'INDEPENDENT-REOPEN.v1.json'
    assert probe.job.sha(receipt) == prior['independent_receipt_sha256']
    proof = probe.job.read(receipt)
    probe.job.check(proof['input_pins'])
    assert probe.job.sha(OLD / 'GPU.v1.json') == proof['capture_sha256']
    helper = helper_source()
    if HELPER.exists():
        assert HELPER.read_text('utf-8') == helper, 'Preserve existing Built-in helper'
    else:
        HELPER.write_text(helper, 'utf-8')
    functions = [n for n in ast.parse(BASE.read_text('utf-8-sig')).body
        if isinstance(n, ast.FunctionDef) and n.name in {'prepare', 'run'}]
    code = ast.unparse(ast.Module(body=functions, type_ignores=[]))
    code = code.replace('LegacyParticleShaderGpuProbe.cs', 'LegacyParticleShaderGpuProbeBuiltin.cs')
    code = code.replace('OpenRPGLegacyParticleShaderGpuProbe.Export', 'OpenRPGLegacyParticleShaderGpuProbeBuiltin.Export')
    anchor = 'job.check(pins)'
    assert code.count(anchor) == 1
    code = replace_once(code, anchor, '''
    for extra in [Path(__file__).resolve(), BASE, HERE/'queue_delta_firepit_effect_materials_v1.py', HERE/'candidates/LegacyParticleShaderGpuProbe.cs', OLD/'GPU.v1.json', OLD/'RUN-RESULT.v1.json', OLD/'INDEPENDENT-REOPEN.v1.json', job.PROJECT/'ProjectSettings/GraphicsSettings.asset', job.PROJECT/'ProjectSettings/QualitySettings.asset']:
        pins[str(extra)]=job.sha(extra)
    job.save('PRIOR-API-ADMISSION.v1.json',dict(prior_capture_sha256=job.sha(OLD/'GPU.v1.json'),prior_observations_retained=True,prior_matrix_api_context_qualified=False,reason='SetViewProjectionMatrices is documented for Built-in only; prior project configured HDRP; repeat under explicit temporary Built-in context and restore both pipeline settings',api_documentation='https://docs.unity.com/ja-jp/engine/6000.5/script-reference/unityengine/rendering/commandbuffer/setviewprojectionmatrices',full_import=False))
    pins[str(ROOT/'PRIOR-API-ADMISSION.v1.json')]=job.sha(ROOT/'PRIOR-API-ADMISSION.v1.json')
    job.check(pins)''')
    code = code.replace('capturing_16_original_legacy_shader_gpu_fixtures', 'capturing_16_original_shader_fixtures_in_supported_builtin_context')
    code = code.replace('16_original_legacy_shader_gpu_fixtures_reopened', '16_original_shader_gpu_fixtures_reopened_in_supported_builtin_context')
    namespace = dict(probe.__dict__)
    namespace.update(__file__=str(Path(__file__).resolve()), HELPER=HELPER, BASE=BASE, OLD=OLD, verify=verify)
    exec(compile(code, str(Path(__file__).resolve()), 'exec'), namespace)
    namespace['run' if len(sys.argv) > 1 and sys.argv[1] == 'run' else 'prepare']()

if __name__ == '__main__':
    main()
