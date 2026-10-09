"""Prepare original legacy material bindings from qualified native tint pixels."""
from pathlib import Path
import datetime
import hashlib
import json
import shutil
import struct

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-firepit-legacy-material-bindings-v1'

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def linear(v):
    assert 0 <= v <= 1
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4

def main():
    assert not ROOT.exists()
    tint_path = W / 'delta-firepit-native-tint-gpu-v3/RESULT.v1.json'
    tint = read(tint_path)
    assert tint['unique_uniform_tint_hypothesis_qualified']
    assert tint['matching_hypotheses'] == ['linearized_get_color']
    pins = dict(tint['input_pins'])
    material_path = W / 'unity-delta-firepit-effect-materials-v1/MATERIALS.v1.json'
    shader_path = W / 'delta-firepit-alpha-shader-port-v1/RESULT.v1.json'
    shader = read(shader_path)
    pins.update(shader['input_pins'])
    source_proof_path = W / 'delta-complete-prefab-source-reopen-v1/RESULT.v1.json'
    source_proof = read(source_proof_path)
    pins.update(source_proof['input_pins'])
    pins.update(source_proof['output_pins'])
    for p in (tint_path, material_path, shader_path, source_proof_path, Path(__file__).resolve()):
        pins[str(p)] = sha(p)
    for p, expected in pins.items():
        assert sha(Path(p)) == expected, 'changed input: ' + p
    ROOT.mkdir()
    library = Path(shader['shader_library'])
    shutil.copyfile(library, ROOT / library.name)
    licenses = []
    for index, p in enumerate(sorted(library.parent.glob('LICENSE.Unity.*.txt'))):
        destination = ROOT / f'LICENSE.Unity.{index:02d}.txt'
        shutil.copyfile(p, destination)
        assert sha(destination) == sha(p)
        licenses.append(str(destination))
    assert licenses, 'retain shader license'
    output_pins = {str(ROOT / library.name): sha(ROOT / library.name)}
    output_pins.update({p: sha(Path(p)) for p in licenses})
    rows = []
    native = read(material_path)
    for case in native['cases']:
        guid = case['material_guid']
        ir_path = next(p for p in (W / 'delta-complete-prefab-source-reopen-v1').glob('*-SOURCE-IR.v1.json')
                       if read(p)['material_guid'] == guid)
        ir = read(ir_path)
        assert case['shader_file_id'] == '203' and case['shader_name'] == 'Legacy Shaders/Particles/Alpha Blended'
        properties = {p['name']: p for p in case['properties']}
        assert set(properties) == {'_TintColor', '_MainTex', '_InvFade'}
        texture = properties['_MainTex']
        dependency = next(d for d in ir['material_dependencies'] if
            d['field_path'].endswith('._MainTex.m_Texture'))
        assert dependency['source_reference']['guid'] == texture['texture_guid']
        assert dependency['source_reference']['fileID'] == texture['texture_file_id'] == '2800000'
        assert sha(Path(dependency['source'])) == dependency['source_sha256']
        source_tint = properties['_TintColor']['vector']
        values = [linear(source_tint[k]) for k in 'xyz'] + [source_tint['w']]
        values += [texture['texture_scale_offset'][k] for k in 'xyzw']
        values += [properties['_InvFade']['number'], 0, 0, 0]
        raw = struct.pack('<12f', *values)
        uniform = struct.unpack('<12f', raw)
        destination = ROOT / guid
        destination.mkdir()
        binary = destination / 'MATERIAL-UNIFORMS.bin'
        binary.write_bytes(raw)
        binding = dict(schema_version=1, material_guid=guid, shader_family='unity_legacy_particles_alpha_blended',
            original_shader_guid=case['shader_guid'], original_shader_file_id=case['shader_file_id'],
            original_material_source=ir['material_source'], original_material_documents=ir['original_material_documents'],
            original_keyword_list=case['keywords'], source_tint_get_color=source_tint,
            gpu_tint_linear_f32=list(uniform[:4]), source_maintex_scale_offset=texture['texture_scale_offset'],
            maintex_scale_offset_f32=list(uniform[4:8]), source_inv_fade=properties['_InvFade']['number'],
            source_texture=dependency, source_texture_meta_sha256=sha(Path(dependency['source'] + '.meta')),
            shader_library_sha256=sha(ROOT / library.name), shader_license_paths=licenses,
            uniform_layout=dict(bytes=48, little_endian_f32=True, tint_offset=0, maintex_st_offset=16,
                inv_fade_offset=32, last_three_floats_are_padding=True),
            uniform_blob_sha256=sha(binary),
            source_shader_render_state=shader['original_shader_render_state'],
            inactive_saved_texture_aliases_retained_in_original_documents=True,
            native_tint_fixture_proof_sha256=sha(tint_path),
            native_imported_texture_storage_required=True,
            actual_source_texture_sampling_qualified=False, color_mask_installed_in_renderer=False,
            billboard_runtime_converted=False, soft_particle_and_fog_variants_qualified=False,
            renderer_admission=False, studio_verified=False, full_import=False)
        metadata = destination / 'binding.v1.json'
        metadata.write_text(json.dumps(binding, indent=2), 'utf-8')
        reopened = read(metadata)
        assert sha(binary) == reopened['uniform_blob_sha256']
        assert len(binary.read_bytes()) == 48
        assert list(struct.unpack('<12f', binary.read_bytes())[:4]) == reopened['gpu_tint_linear_f32']
        output_pins.update({str(p): sha(p) for p in (binary, metadata)})
        rows.append(dict(material_guid=guid, binding=str(metadata), uniform_blob=str(binary),
            active_texture_guid=texture['texture_guid'], renderer_admission=False))
    assert len(rows) == 2
    for p, expected in pins.items():
        assert sha(Path(p)) == expected, 'changed input after bindings: ' + p
    result = dict(status='two_original_legacy_material_bindings_prepared_and_reopened',
        material_bindings=rows, input_pins=pins, output_pins=output_pins,
        native_tint_conversion_qualified=True, actual_source_texture_sampling_qualified=False,
        renderer_installed=False, whole_effect_qualified=False, full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(dict(status=result['status'], bindings=2, uniform_bytes_each=48, full_import=False)))

if __name__ == '__main__':
    main()
