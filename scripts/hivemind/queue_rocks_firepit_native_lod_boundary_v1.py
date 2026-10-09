"""Measure original native LOD thresholds and adjacent float32 values on private clones."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os
import subprocess
import sys
import queue_rocks_firepit_native_lod_metric_v1 as metric

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'rocks-firepit-native-lod-boundary-v1'
PROJECT = metric.PROJECT
HELPER = PROJECT / 'Assets/Editor/RocksFirePitNativeLODBoundaryV1.cs'


def replace_once(text, old, new):
    assert text.count(old) == 1, 'Original helper preimage changed'
    return text.replace(old, new, 1)


CODE = metric.CODE.replace('OpenRPGRocksFirePitNativeLODMetricV1', 'OpenRPGRocksFirePitNativeLODBoundaryV1')
CODE = replace_once(CODE, 'public float requested_relative_height;',
                    'public int source_level_index;public float source_threshold;public string source_threshold_bits;public int neighbor_delta;public string requested_relative_height_bits;public float requested_relative_height;')
CODE = replace_once(CODE, 'static float[] Vec(Vector3 v)',
                    'static float Neighbor(float value,int delta){if(value<=0||float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Positive finite original threshold required");uint bits=BitConverter.ToUInt32(BitConverter.GetBytes(value),0);return BitConverter.ToSingle(BitConverter.GetBytes((uint)((long)bits+delta)),0);}\nstatic float[] Vec(Vector3 v)')
CODE = replace_once(CODE, 'foreach(float height in new[]{0.01f,0.03f,0.1f,0.18f,0.25f,0.35f,0.6f}){',
                    'var levels=group.GetLODs();for(int levelIndex=0;levelIndex<levels.Length;levelIndex++){float threshold=levels[levelIndex].screenRelativeTransitionHeight;for(int delta=-1;delta<=1;delta++){float height=Neighbor(threshold,delta);')
CODE = replace_once(CODE, 'requested_relative_height=height,distance=d,',
                    'source_level_index=levelIndex,source_threshold=threshold,source_threshold_bits=Bits(threshold),neighbor_delta=delta,requested_relative_height_bits=Bits(height),requested_relative_height=height,distance=d,')
CODE = replace_once(CODE, '    }\n   }\n  }}finally', '    }}\n   }\n  }}finally')


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def save(name, value):
    path = ROOT / name
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(value, indent=2), 'utf-8')
    temporary.replace(path)


def prepare():
    assert not ROOT.exists() and not HELPER.exists()
    pwsh = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
    assert subprocess.check_output([pwsh, '-NoProfile', '-Command', '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],
                                   text=True, creationflags=subprocess.CREATE_NO_WINDOW).strip() == '0'
    source = W / 'hivemind-run/native-american-village/source'
    assembly = W / 'rocks-firepit-scene-bindings-v1/RESULT.v1.json'
    files = [Path(__file__).resolve(), Path(metric.__file__), assembly]
    for name in ('SM_rocks_002', 'SM_FirePit_01'):
        relative = f'Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Prefabs/{name}.prefab'
        for suffix in ('', '.meta'):
            original, private = source / (relative + suffix), PROJECT / (relative + suffix)
            assert sha(original) == sha(private)
            files += [original, private]
    assert not list((PROJECT / 'Assets/Hivemind').rglob('*.cs')), 'Purchased scripts must remain absent'
    ROOT.mkdir()
    HELPER.write_text(CODE, 'utf-8')
    files.append(HELPER)
    files += [PROJECT / p for p in ('ProjectSettings/GraphicsSettings.asset', 'ProjectSettings/QualitySettings.asset',
                                    'ProjectSettings/ProjectSettings.asset', 'Packages/manifest.json')]
    expected = sum(len(group['levels']) * 9 for scene in read(assembly)['rows'] for group in scene['original_lod_groups'])
    save('PREPARED.v1.json', dict(input_pins={str(p): sha(p) for p in files}, expected_samples=expected,
         source_scripts_executed=False, private_original_prefab_clones=True, prefab_instantiated=True,
         source_model_authored=False, camera_rendered=False, particle_simulation_executed=False, full_import=False))
    with (ROOT / 'controller.log').open('xb') as output:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
                                  stdin=subprocess.DEVNULL, stdout=output, stderr=output, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(controller_pid=child.pid, expected_samples=expected))
    print(json.dumps(dict(controller_pid=child.pid, expected_samples=expected)))


def run():
    state = dict(status='querying_native_lod_threshold_boundaries', full_import=False)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        for path, expected in prepared['input_pins'].items():
            assert sha(path) == expected
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), OPENRPG_LOD_METRIC_OUTPUT=str(ROOT),
                   UPM_CACHE_ROOT=str(W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/upm-cache'))
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            child = subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe', '-batchmode', '-nographics', '-quit',
                '-projectPath', str(PROJECT), '-executeMethod', 'OpenRPGRocksFirePitNativeLODBoundaryV1.Capture.Export',
                '-logFile', str(ROOT / 'unity.log')], cwd=ROOT, env=env, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state['unity_pid'] = child.pid
            save('RUN-RESULT.v1.json', state)
            state['actual_unity_exit_code'] = child.wait()
        assert state['actual_unity_exit_code'] == 0, 'Preserve native diagnostics; no approximate fallback'
        native = read(ROOT / 'NATIVE-RESULT.v1.json')
        assert len(native['samples']) == prepared['expected_samples']
        for path, expected in prepared['input_pins'].items():
            assert sha(path) == expected
        state.update(status='native_lod_threshold_boundaries_captured_reopen_pending', samples=len(native['samples']),
                     native_result_sha256=sha(ROOT / 'NATIVE-RESULT.v1.json'), input_pins_unchanged=True)
    except Exception as exc:
        state.update(status='native_boundary_capture_refused', error=repr(exc))
    state['finished_utc'] = datetime.now(timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
