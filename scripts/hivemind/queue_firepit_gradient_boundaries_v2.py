"""Initialize a native measurement clone; retain strict particle/count checks."""
from pathlib import Path
import ast
import importlib.util

HERE = Path(__file__).resolve().parent
BASE = HERE / 'queue_firepit_gradient_boundaries_v1.py'
spec = importlib.util.spec_from_file_location('gradient_boundaries_v1', BASE)
probe = importlib.util.module_from_spec(spec); spec.loader.exec_module(probe)
probe.__file__ = str(Path(__file__).resolve())
probe.ROOT = probe.probe.W / 'unity-delta-firepit-gradient-boundaries-v2'
probe.probe.job.ROOT = probe.ROOT
original_helper = probe.helper_source

def helper_source():
    text = original_helper().replace('OpenRPGFirePitGradientBoundaries', 'OpenRPGFirePitGradientBoundariesV2')
    text = probe.once(text, 'ps.SetParticles(new[]{supplied},1);',
        'ps.Simulate(0,false,true,true);ps.SetParticles(new[]{supplied},1);')
    text = probe.once(text, 'throw new Exception("Native supplied particle missing")',
        'throw new Exception("Native supplied particle missing; node="+c.node_path+"; repetition="+repetition+"; age="+time.ToString("R",CultureInfo.InvariantCulture)+"; count="+count+"; lifetime="+supplied.startLifetime.ToString("R",CultureInfo.InvariantCulture)+"; remaining="+supplied.remainingLifetime.ToString("R",CultureInfo.InvariantCulture))')
    text = probe.once(text, 'no Simulate/Play, autonomous/prewarm or original RNG qualification',
        'zero-duration Simulate initialization before SetParticles; no Play, autonomous/prewarm or original RNG qualification')
    return text

probe.helper_source = helper_source
if __name__ == '__main__':
    prior = probe.probe.read(probe.probe.W / 'unity-delta-firepit-gradient-boundaries-v1/RUN-RESULT.v1.json')
    assert prior.get('finished_utc') and not prior.get('native_gradient_lookup_probe_verified')
    node = next(n for n in ast.parse(BASE.read_text('utf-8')).body if isinstance(n, ast.FunctionDef) and n.name == 'main')
    code = ast.unparse(node).replace('OpenRPGFirePitGradientBoundaries', 'OpenRPGFirePitGradientBoundariesV2')
    code = code.replace('native_frames=112', 'native_frames=len(manifest["times"])*4')
    code = code.replace('geometry_observations=336', 'geometry_observations=len(manifest["times"])*4')
    exec(compile(code, str(Path(__file__).resolve()), 'exec'), probe.__dict__)
    probe.main()
