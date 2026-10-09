"""Capture the original legacy pass with its real RA texture, all HDR pixels.

Retain white-texture fixtures. Diagnostic quad is measurement geometry only.
Source materials/shader/importers remain pinned; no whole-effect claim.
"""
from pathlib import Path
import ast
import importlib.util
import math
import struct
import sys

HERE = Path(__file__).resolve().parent
BASE = HERE / 'queue_firepit_legacy_shader_gpu_v1.py'
spec = importlib.util.spec_from_file_location('native_ra_probe', BASE)
probe = importlib.util.module_from_spec(spec); spec.loader.exec_module(probe)
ROOT = probe.W / 'unity-delta-firepit-native-ra-shader-v1'
probe.job.ROOT = ROOT
HELPER = HERE / 'candidates/LegacyParticleNativeRaShader.cs'
BUFFER_PINS = {}

def once(text, old, new):
    assert text.count(old) == 1, 'Native helper shape differs: ' + old
    return text.replace(old, new)

def helper():
    text = (HERE / 'candidates/LegacyParticleShaderGpuProbeBuiltinHalf.cs').read_text('utf-8')
    text = text.replace('OpenRPGLegacyParticleShaderGpuProbeBuiltinHalf', 'OpenRPGLegacyParticleNativeRaShader')
    text = once(text, 'public class Row {', 'public class Row {public string pixels_path,pixels_sha256;public int width=16,height=16;')
    text = once(text, 'public class Case {', 'public class Case {public string texture_guid,texture_file_id,graphics_format,texture_format,filter_mode,wrap_mode_u,wrap_mode_v;public int texture_width,texture_height,mipmap_count,anisotropy;public float mip_bias;')
    text = once(text, 'temporary white linear texture;', 'actual bound RA texture, all 16x16 HDR pixels;')
    text = once(text, 'no source texture or whole-effect rendering qualification', 'no whole-effect, soft/fog or Forge qualification')
    text = once(text, 'var results=new List<Case>();var texture=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);texture.SetPixel(0,0,Color.white);texture.Apply(false,false);',
        'var results=new List<Case>();bool observedSourceFragment=false;')
    text = once(text, 'var readback=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);',
        'var readback=new Texture2D(16,16,TextureFormat.RGBAHalf,false,true);')
    text = once(text, 'string before=EditorJsonUtility.ToJson(source,true);', '''var texture=source.GetTexture("_MainTex") as Texture2D;if(texture==null)throw new Exception("Actual source RA texture missing");Identity(texture,"63f624c8bd42f35409dde06b85d2d900","2800000");
    string before=EditorJsonUtility.ToJson(source,true);''')
    text = once(text, 'material.SetTexture("_MainTex",texture);', '// Original cloned material retains its actual bound texture.\n     ')
    text = once(text, 'rows.Add(new Row{', '''var pixels=readback.GetPixels();if(pixels.Length!=256)throw new Exception("Native pixel count differs");
      foreach(var pixel in pixels){if(float.IsNaN(pixel.r)||float.IsInfinity(pixel.r)||float.IsNaN(pixel.g)||float.IsInfinity(pixel.g)||float.IsNaN(pixel.b)||float.IsInfinity(pixel.b)||float.IsNaN(pixel.a)||float.IsInfinity(pixel.a)||pixel.a!=background.a)throw new Exception("Native HDR pixels/alpha differ");if(background.r==0&&(pixel.r>0||pixel.g>0||pixel.b>0))observedSourceFragment=true;}
      var raw=readback.GetRawTextureData<byte>().ToArray();if(!BitConverter.IsLittleEndian||raw.Length!=2048)throw new Exception("Native half pixel storage differs");
      var directory=Path.Combine(Path.GetDirectoryName(manifest.output),"native-pixels");Directory.CreateDirectory(directory);string pixelPath=Path.Combine(directory,input.material_guid+"-"+rows.Count.ToString("D2")+".rgba.f16");if(File.Exists(pixelPath))throw new Exception("Preserve earlier native pixels");File.WriteAllBytes(pixelPath,raw);
      string pixelSha;using(var hashing=System.Security.Cryptography.SHA256.Create())pixelSha=BitConverter.ToString(hashing.ComputeHash(raw)).Replace("-","").ToLowerInvariant();
      rows.Add(new Row{pixels_path=pixelPath,pixels_sha256=pixelSha,''')
    text = once(text, 'new Case{material_guid=', '''new Case{texture_guid="63f624c8bd42f35409dde06b85d2d900",texture_file_id="2800000",graphics_format=texture.graphicsFormat.ToString(),texture_format=texture.format.ToString(),filter_mode=texture.filterMode.ToString(),wrap_mode_u=texture.wrapModeU.ToString(),wrap_mode_v=texture.wrapModeV.ToString(),texture_width=texture.width,texture_height=texture.height,mipmap_count=texture.mipmapCount,anisotropy=texture.anisoLevel,mip_bias=texture.mipMapBias,material_guid=''')
    text = once(text, 'if(!results.Any(c=>c.rows.Any(r=>r.background.r==0&&r.result.r>0)))', 'if(!observedSourceFragment)')
    text = once(text, 'UnityEngine.Object.DestroyImmediate(texture);', '')
    text = text.replace('// temporary material clone to isolate native tint/blending/channel semantics.',
        '// temporary material clone, retaining the actual source texture and sampler.')
    text = text.replace('// Measurement fixtures only: retain the real shader, override its texture on a',
        '// Measurement fixtures only: retain the real shader and bound texture on a')
    return text

def verify(report):
    assert report['measurement_render_pipeline'] == 'Builtin' and report['pipeline_settings_restored']
    assert report['render_target_format'] == 'ARGBHalf' and report['color_space'] == 'Linear'
    assert report['gpu_type'] != 'Null' and report['source_materials_unchanged']
    assert report['source_shader_fixture_gpu_captured'] and not report['full_import']
    assert len(report['cases']) == 2
    manifest = probe.job.read(ROOT / 'manifest.json')
    identities = probe.job.read(probe.W / 'unity-delta-firepit-effect-materials-v1/MATERIALS.v1.json')
    observed = 0; positive = False
    for case in report['cases']:
        source = next(c for c in identities['cases'] if c['material_guid'] == case['material_guid'])
        assert case['shader_guid'] == source['shader_guid'] and case['shader_file_id'] == '203'
        assert case['shader_name'] == 'Legacy Shaders/Particles/Alpha Blended'
        assert case['original_keywords'] == source['keywords']
        assert case['texture_guid'] == '63f624c8bd42f35409dde06b85d2d900' and case['texture_file_id'] == '2800000'
        assert case['graphics_format'] == 'RGBA_DXT5_SRGB' and case['texture_format'] == 'DXT5'
        assert case['filter_mode'] == 'Bilinear' and case['wrap_mode_u'] == case['wrap_mode_v'] == 'Repeat'
        assert case['mipmap_count'] == 11 and case['anisotropy'] == 1 and case['mip_bias'] == 0
        assert case['texture_width'] == case['texture_height'] == 1024
        inputs = next(c for c in manifest['cases'] if c['material_guid'] == case['material_guid'])
        assert len(case['rows']) == 8
        for index, row in enumerate(case['rows']):
            assert row['baked_color'] == inputs['baked_colors'][index // 2]
            background = dict(r=0, g=0, b=0, a=.25) if index % 2 == 0 else dict(r=.125, g=.25, b=.5, a=.75)
            assert row['background'] == background and row['alpha_channel_preserved']
            assert row['width'] == row['height'] == 16
            path = Path(row['pixels_path'])
            assert path.resolve().is_relative_to((ROOT / 'native-pixels').resolve())
            raw = path.read_bytes(); assert len(raw) == 2048 and probe.job.sha(path) == row['pixels_sha256']
            pixels = list(struct.iter_unpack('<4e', raw)); assert len(pixels) == 256
            assert all(all(math.isfinite(v) for v in pixel) and pixel[3] == background['a'] for pixel in pixels)
            center = pixels[8*16+8]
            assert list(center) == [row['result'][channel] for channel in 'rgba']
            if index % 2 == 0: positive |= any(any(v > 0 for v in pixel[:3]) for pixel in pixels)
            BUFFER_PINS[str(path)] = probe.job.sha(path); observed += 1
    assert observed == 16 and positive
    probe.job.check(BUFFER_PINS)
    return observed

def main():
    text = helper()
    if HELPER.exists(): assert HELPER.read_text('utf-8') == text, 'Preserve native helper'
    else: HELPER.write_text(text, 'utf-8')
    functions = [n for n in ast.parse(BASE.read_text('utf-8')).body
        if isinstance(n, ast.FunctionDef) and n.name in {'prepare', 'run'}]
    code = ast.unparse(ast.Module(body=functions, type_ignores=[]))
    code = code.replace('LegacyParticleShaderGpuProbe.cs', 'LegacyParticleNativeRaShader.cs')
    code = code.replace('OpenRPGLegacyParticleShaderGpuProbe.Export', 'OpenRPGLegacyParticleNativeRaShader.Export')
    code = once(code, 'job.check(pins)', '''
    for extra in [BASE,HERE/'candidates/LegacyParticleShaderGpuProbeBuiltinHalf.cs',job.PROJECT/'ProjectSettings/GraphicsSettings.asset',job.PROJECT/'ProjectSettings/QualitySettings.asset',W/'unity-delta-firepit-active-texture-storage-v1/INDEPENDENT-REOPEN.v1.json',W/'delta-firepit-ra-sampling-parity-v1/COMPARISON.v1.json']:
        pins[str(extra)]=job.sha(extra)
    job.check(pins)''')
    code = code.replace('source_texture_override_fixture_only=True', 'source_texture_override_fixture_only=False, actual_bound_ra_texture=True')
    code = code.replace('gpu_fixtures_verified=count,', 'gpu_fixtures_verified=count, pixels_verified=4096, output_pins=BUFFER_PINS.copy(), actual_source_texture_native_rendering_captured=True,')
    code = code.replace('capturing_16_original_legacy_shader_gpu_fixtures', 'capturing_actual_ra_shader_and_4096_hdr_pixels')
    code = code.replace('16_original_legacy_shader_gpu_fixtures_reopened', 'actual_ra_shader_and_4096_hdr_pixels_reopened')
    namespace = dict(probe.__dict__)
    namespace.update(__file__=str(Path(__file__).resolve()), ROOT=ROOT, HELPER=HELPER,
        BASE=BASE, verify=verify, BUFFER_PINS=BUFFER_PINS)
    exec(compile(code, str(Path(__file__).resolve()), 'exec'), namespace)
    namespace['run' if len(sys.argv) > 1 and sys.argv[1] == 'run' else 'prepare']()

if __name__ == '__main__': main()
