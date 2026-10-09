"""Measure native lifetime/color lookup boundaries on isolated original clones.

One manually supplied original-type particle, no approximate simulation.
All sources, inherited parameters, transforms and materials retained.
"""
from pathlib import Path
import ast
import copy
import importlib.util
import math
import struct
import sys

HERE = Path(__file__).resolve().parent
BASE = HERE / 'queue_firepit_particle_geometry_v1.py'
spec = importlib.util.spec_from_file_location('gradient_geometry_job', BASE)
probe = importlib.util.module_from_spec(spec); spec.loader.exec_module(probe)
ROOT = probe.W / 'unity-delta-firepit-gradient-boundaries-v1'
probe.job.ROOT = ROOT

def once(text, old, new):
    assert text.count(old) == 1, 'Source protocol shape differs: ' + old
    return text.replace(old, new)
def f32(value): return struct.unpack('<f', struct.pack('<f',value))[0]
def ages():
    # Exact uniform grid, interval midpoints, and adjacent float32 values.
    values = {0.0}
    for index in range(257):
        value = f32(index/256)
        bits = struct.unpack('<I',struct.pack('<f',value))[0]
        for delta in [-1,0,1]:
            if 0 <= bits+delta < 0x3f800000:
                sample = struct.unpack('<f',struct.pack('<I',bits+delta))[0]
                if 0 <= sample < 1: values.add(sample)
        if index < 256: values.add(f32((index+.5)/256))
    return sorted(values)

def helper_source():
    text = probe.helper_source().replace('OpenRPGFirePitParticleGeometry','OpenRPGFirePitGradientBoundaries')
    text = text.replace('requested_time','requested_normalized_age')
    text = once(text, 'public Geometry[] geometry;', 'public Geometry[] geometry;public Color gradient_evaluation;public float normalized_age_from_particle;')
    text = once(text, 'var points=new[]{new Vector3(0,2,-5),new Vector3(-5,2,0),new Vector3(0,5,-2)};',
        'var points=new[]{new Vector3(0,2,-5)};')
    text = once(text, 'if(time<0||float.IsNaN(time)||float.IsInfinity(time))',
        'if(time<0||time>=1||float.IsNaN(time)||float.IsInfinity(time))')
    text = once(text, 'ps.Simulate(time,false,true,true);', '''
     if(ps.main.startLifetime.mode!=ParticleSystemCurveMode.Constant||ps.main.startSize.mode!=ParticleSystemCurveMode.Constant||ps.main.startColor.mode!=ParticleSystemGradientMode.Color||ps.colorOverLifetime.color.mode!=ParticleSystemGradientMode.Gradient)throw new Exception("Original diagnostic source modes differ");
     var originalStart=(Color32)ps.main.startColor.color;if(originalStart.r!=255||originalStart.g!=255||originalStart.b!=255||originalStart.a!=255)throw new Exception("Do not fabricate start color");
     var supplied=new ParticleSystem.Particle{position=Vector3.zero,velocity=Vector3.zero,rotation=0,startSize=ps.main.startSize.constant,startLifetime=ps.main.startLifetime.constant,startColor=originalStart,randomSeed=seed};
     supplied.remainingLifetime=supplied.startLifetime*(1-time);if(supplied.remainingLifetime<=0)throw new Exception("Diagnostic lifetime ended after float32 rounding");ps.SetParticles(new[]{supplied},1);''')
    text = once(text, 'int count=ps.GetParticles(buffer);', 'int count=ps.GetParticles(buffer);if(count!=1)throw new Exception("Native supplied particle missing");')
    text = once(text, 'rows.Add(new Frame{geometry=BakeGeometry(ps),', '''float normalizedAge=1-buffer[0].remainingLifetime/buffer[0].startLifetime;
     rows.Add(new Frame{gradient_evaluation=ps.colorOverLifetime.color.gradient.Evaluate(normalizedAge),normalized_age_from_particle=normalizedAge,geometry=BakeGeometry(ps),''')
    start = 'simulation_protocol="isolated complete-parent clone;'
    end = 'simultaneous-system parity claim"'
    a = text.index(start); b = text.index(end,a)+len(end)
    text = text[:a] + 'simulation_protocol="isolated complete-parent clone; Stop(clear) siblings; fixed seed; one explicitly supplied particle with original constant start lifetime/size/color; normalized lifetime boundary sweep; no Simulate/Play, autonomous/prewarm or original RNG qualification"' + text[b:]
    return text

def verify(report, prepared):
    manifest = probe.read(ROOT/'manifest.json')
    baseline = probe.read(probe.W/'unity-delta-firepit-particle-geometry-v1/GEOMETRY.v1.json')
    expected = len(manifest['times'])*manifest['repetitions']*2
    frames = 0; vertices = 0
    assert len(report['cases']) == 2 and not report['full_import']
    for case in report['cases']:
        old = next(c for c in baseline['cases'] if c['node_path']==case['node_path'])
        for field in ['prefab_guid','particle_file_id','renderer_file_id','material_guid','material_file_id',
            'original_duration','original_simulation_speed','original_loop','original_prewarm','original_play_on_awake','active_color_space']:
            assert case[field] == old[field], 'Changed original source field: '+field
        assert case['active_color_space'] == 'Linear'
        old_system = probe.job.json.loads(old['native_particle_json'])['ParticleSystem']
        new_system = probe.job.json.loads(case['native_particle_json'])['ParticleSystem']
        for module in ['ColorModule','InitialModule']: assert new_system[module] == old_system[module]
        assert len(case['rows']) == len(manifest['times'])*2
        by_time = {}
        for row in case['rows']:
            assert row['seed'] == 1729 and row['count'] == len(row['particles']) == 1
            assert row['repetition'] in [0,1] and row['requested_normalized_age'] in manifest['times']
            assert len(row['geometry']) == 1
            particle = row['particles'][0]; assert all(particle['start_color'][k]==255 for k in 'rgba')
            expected_age = f32(1-f32(particle['remaining_lifetime']/particle['start_lifetime']))
            assert row['normalized_age_from_particle'] == expected_age
            geometry = row['geometry'][0]; assert geometry['fixture'] == 0 and not geometry['use_transform']
            assert geometry['vertices'] == 4 and len(geometry['colors']) == 4
            assert len(geometry['indices']) == 6 and all(c==geometry['colors'][0] for c in geometry['colors'])
            assert geometry['render_mode'] == 'Billboard' and geometry['alignment'] == 'View'
            assert all(math.isfinite(value) for value in row['gradient_evaluation'].values())
            stripped = copy.deepcopy(row); stripped.pop('repetition')
            time = row['requested_normalized_age']
            if time in by_time: assert by_time[time] == stripped, 'Native boundary sweep not repeatable'
            else: by_time[time] = stripped
            frames += 1; vertices += 4
        assert len(by_time) == len(manifest['times'])
    assert frames == expected
    probe.job.check(prepared['input_pins'])
    return dict(boundary_frames_verified=frames,
        normalized_age_inputs=len(manifest['times']), vertex_observations=vertices,
        repetitions_byte_equal=True, autonomous_runtime_qualified=False)

def main():
    functions = [n for n in ast.parse(BASE.read_text('utf-8')).body
        if isinstance(n,ast.FunctionDef) and n.name in {'prepare','run'}]
    code = ast.unparse(ast.Module(body=functions,type_ignores=[]))
    code = code.replace('OpenRPGFirePitParticleGeometry','OpenRPGFirePitGradientBoundaries')
    code = code.replace('GEOMETRY.v1.json','GRADIENT-BOUNDARIES.v1.json')
    code = code.replace('native_geometry_verified','native_gradient_lookup_probe_verified')
    code = once(code, "manifest['output'] = str(ROOT / 'GRADIENT-BOUNDARIES.v1.json')",
        "manifest['times']=ages(); manifest['seeds']=[1729]; manifest['repetitions']=2\n    manifest['output'] = str(ROOT / 'GRADIENT-BOUNDARIES.v1.json')")
    code = once(code, 'job.check(pins)', '''
    for extra in [BASE, HERE/'queue_delta_firepit_effect_materials_v1.py',W/'unity-delta-firepit-particle-geometry-v1/GEOMETRY.v1.json',W/'delta-firepit-gradient-lookup-v1/RESULT.v1.json']:
        pins[str(extra)]=sha(extra)
    job.check(pins)''')
    code = code.replace('expected_frames=112', 'expected_frames=len(manifest["times"])*4')
    code = code.replace('expected_geometry_observations=336', 'expected_geometry_observations=len(manifest["times"])*4')
    code = code.replace('source_protocol_retained=True', 'source_parameters_retained=True, manual_lifetime_boundary_protocol=True')
    code = code.replace('capturing_336_native_billboard_and_atlas_observations','capturing_original_gradient_lifetime_boundaries')
    code = code.replace('336_native_billboard_and_atlas_observations_reopened','original_gradient_lifetime_boundaries_reopened')
    namespace = dict(probe.__dict__)
    namespace.update(__file__=str(Path(__file__).resolve()),ROOT=ROOT,BASE=BASE,helper_source=helper_source,verify=verify,ages=ages)
    exec(compile(code,str(Path(__file__).resolve()),'exec'),namespace)
    namespace['run' if len(sys.argv)>1 and sys.argv[1]=='run' else 'prepare']()

if __name__ == '__main__': main()
