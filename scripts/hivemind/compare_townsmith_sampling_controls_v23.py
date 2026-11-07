"""Compare unchanged source materials under explicit scene sampler controls."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import numpy as np

W = Path('F:/OpenRPG_ImportWork')
SRC = W / 'townsmith-door-source-anisotropy-controls-v23'
OUT = W / 'townsmith-door-sampling-controls-reopen-v23'
assert not OUT.exists()


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


run = read(SRC / 'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0 and run['cases_captured'] == 3
assert run['neutral_reference_byte_equal'] and run['source_inputs_unchanged']
assert sha(SRC / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
prep = read(SRC / 'PREPARED.v1.json')
pins = dict(prep['input_pins']);pins.update(prep['private_before_pins'])
for p in SRC.iterdir():
    if p.is_file() and p.suffix in ('.json', '.rgba32f', '.rgba16f', '.r32f'):
        pins[str(p)] = sha(p)
baseline_path = W / 'townsmith-door-source-gbuffer-arrays-v21/door-nopost-linear._GBufferTexture1.rgba32f'
assert sha(SRC / 'door-nopost-linear._GBufferTexture1.rgba32f') == sha(baseline_path)
pins[str(baseline_path)] = sha(baseline_path)
native = []
for directory, policy in (('townsmith-paint-door-native-normal-roughness-draw-v4', 'UnityForceEnableD3d'), ('townsmith-paint-door-native-sampling-control-v7', 'Authored')):
    root = W / directory
    r = read(root / 'RUN-RESULT.v1.json');receipt = read(root / 'NATIVE-RESULT.v1.json')
    assert r['actual_exit_code'] == 0 and r['gpu_draw_executed']
    assert receipt['scene_sampling_policy'] == policy
    assert sha(root / 'NATIVE-RESULT.v1.json') == r['native_receipt_sha256']
    assert sha(root / 'FORGE-LINEAR.rgba16f') == r['forge_image_sha256']
    pins.update(read(root / 'PREPARED.v1.json')['input_pins'])
    for p in (root / 'RUN-RESULT.v1.json', root / 'NATIVE-RESULT.v1.json', root / 'FORGE-LINEAR.rgba16f'):
        pins[str(p)] = sha(p)
    native.append(np.round(np.fromfile(root / 'FORGE-LINEAR.rgba16f', dtype='<f2').reshape(512, 512, 4).astype(np.float32)*255).astype(np.uint32))
source = []
masks = []
for case, setting in (('door-nopost-linear', 'ForceEnable'), ('door-noaniso-nopost-linear', 'Disable')):
    context = read(SRC / (case + '.context.json'))
    assert context['anisotropic_filtering'] == setting and not context['actual_postprocess_enabled']
    source.append((np.flipud(np.fromfile(SRC / (case + '._GBufferTexture1.rgba32f'), dtype='<f4').reshape(512, 640, 4)[:, :512])*255.5).astype(np.uint32))
    depth = sorted(SRC.glob(case + '.depth-*.r32f'))[-1]
    masks.append(np.flipud(np.fromfile(depth, dtype='<f4').reshape(512, 512)) > 0)
assert np.array_equal(*masks) and int(masks[0].sum()) == 34445
mask = masks[0]
for p, expected in pins.items():
    assert sha(p) == expected, p


def normals(c):
    x = c[:, :, 0] | ((c[:, :, 2] & 15) << 8)
    y = c[:, :, 1] | ((c[:, :, 2] >> 4) << 8)
    xy = np.stack((x, y), axis=-1).astype(np.float64)/4095*2-1
    n = np.concatenate((xy, (1-np.abs(xy).sum(axis=-1))[:, :, None]), axis=-1)
    t = np.maximum(-n[:, :, 2], 0)
    n[:, :, :2] += np.where(n[:, :, :2] >= 0, -t[:, :, None], t[:, :, None])
    return n/np.linalg.norm(n, axis=-1)[:, :, None]


rows = []
for name, a, b in (('Source ForceEnable / native ForceEnable', source[0], native[0]), ('Source Disable / native Authored', source[1], native[1]), ('Source scene anisotropy effect', source[0], source[1]), ('Native scene anisotropy effect', native[0], native[1])):
    na, nb = normals(a), normals(b)
    angles = np.degrees(np.arccos(np.clip(np.sum(na*nb, axis=-1)[mask], -1, 1)))
    roughness = np.abs(a[:, :, 3].astype(np.int32)-b[:, :, 3].astype(np.int32))[mask]
    rows.append(dict(control=name, mean_normal_angle_degrees=float(angles.mean()), max_normal_angle_degrees=float(angles.max()),
                     identical_normal_codes=int(np.all(a[:, :, :3] == b[:, :, :3], axis=-1)[mask].sum()),
                     identical_roughness_codes=int((roughness == 0).sum()), max_roughness_code_error=int(roughness.max())))
pins[str(Path(__file__))] = sha(Path(__file__))
OUT.mkdir();now = datetime.now(timezone.utc).isoformat()
result = dict(status='causal_source_and_native_sampler_controls_compared', finished_utc=now,
              lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.MATERIAUX.SAMPLING', input_pins=pins,
              rehashed_pins=len(pins)-1, rows=rows, original_reference_and_sources_unchanged=True,
              source_materials_not_replaced=True, source_scene_anisotropy_restored_by_helper=True,
              scene_sampling_override_is_diagnostic_only=True, original_source_normal_buffer_byte_equal=True,
              coverage_unchanged_in_causal_source_controls=True, covered_pixels=34445,
              disabled_control_is_not_substitute_for_original_scene=True, pixel_fit_or_threshold_relaxation=False,
              source_sampler_equivalence_qualified=False, full_material_fidelity_qualified=False,
              full_import=False, complete_assets_delivered_increment=0, forge_editor_verified=False,
              next_action='Use measured causal sampler differences to isolate original implicit UV derivatives and sampler state; preserve original ForceEnable scene as the fidelity target.')
(OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
T = W / 'SUIVI_LOTS_HIVEMIND'
summary = '; '.join(f"{r['control']} : angle moyen {r['mean_normal_angle_degrees']:.6f}°" for r in rows[:2]) + '. Contrôles causaux et profondeur source inchangée ; la scène ForceEnable originale reste la cible.'
for name in ('AVANCEMENT_ACTUEL.md', 'PLAN_LOTS_A_SUIVRE.md', 'JOURNAL.md'):
    with (T / name).open('a', encoding='utf-8') as stream:
        stream.write(f'\n- {now} — **SAMPLING : contrôle causal vérifié**. {summary} [Preuve](../{OUT.name}/RESULT.v1.json). Aucun nouvel asset complet livré.\n')
for name in ('PLAN_LOTS_A_SUIVRE.v1.json', 'SUIVI_SIMPLE.v1.json'):
    p = T / name;data = read(p)
    data.update(updated_utc=now,last_technical_batch=result['lot'],last_technical_proof=str(OUT/'RESULT.v1.json'),
                current_work_sub_lot='L03.TOWNSMITH.DESSIN.02.TRANSPORT.DIRECT.MATERIAUX.DERIVEES',current_work_state=summary,next_action=result['next_action'],full_import=False)
    p.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(json.dumps(dict(status=result['status'],pins=result['rehashed_pins'],rows=rows)))
