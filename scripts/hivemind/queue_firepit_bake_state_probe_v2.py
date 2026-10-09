"""Measure live particles after each native bake; keep all previous captures."""
from pathlib import Path
import copy
import importlib.util
import json
import sys

HERE = Path(__file__).resolve().parent
BASE = HERE / 'queue_firepit_particle_geometry_v1.py'
spec = importlib.util.spec_from_file_location('native_geometry_probe', BASE)
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)
OLD = probe.W / 'unity-delta-firepit-particle-geometry-v1'
probe.ROOT = probe.W / 'unity-delta-firepit-bake-state-probe-v2'
probe.job.ROOT = probe.ROOT
old_helper = probe.helper_source

def helper_source():
    text = old_helper().replace('OpenRPGFirePitParticleGeometry', 'OpenRPGFirePitBakeStateProbe')
    anchor = '[Serializable] public class Geometry {'
    assert text.count(anchor) == 1
    text = text.replace(anchor, anchor + 'public Particle[] post_bake_particles;public float native_time_after_bake,fixed_delta_time,delta_time;public int particle_count_after_bake;')
    anchor = 'renderer.BakeMesh(mesh,camera,false);'
    assert text.count(anchor) == 1
    text = text.replace(anchor, anchor + '''
    var postBuffer=new ParticleSystem.Particle[ps.main.maxParticles];int postCount=ps.GetParticles(postBuffer);var postParticles=new Particle[postCount];
    for(int n=0;n<postCount;n++){var p=postBuffer[n];postParticles[n]=new Particle{random_seed=p.randomSeed,position=p.position,velocity=p.velocity,rotation=p.rotation3D,angular_velocity=p.angularVelocity3D,start_size=p.startSize3D,current_size=p.GetCurrentSize3D(ps),start_lifetime=p.startLifetime,remaining_lifetime=p.remainingLifetime,start_color=p.startColor,current_color=p.GetCurrentColor(ps)};}
''')
    anchor = 'rows.Add(new Geometry{fixture=fixture,'
    assert text.count(anchor) == 1
    return text.replace(anchor, 'rows.Add(new Geometry{post_bake_particles=postParticles,native_time_after_bake=ps.time,particle_count_after_bake=postCount,fixed_delta_time=Time.fixedDeltaTime,delta_time=Time.deltaTime,fixture=fixture,')

def verify(report, prepared):
    reference = probe.read(OLD / 'GEOMETRY.v1.json')
    observations = changed = vertices = 0
    clocks = []
    for case in report['cases']:
        original = next(c for c in reference['cases'] if c['node_path'] == case['node_path'])
        stripped = copy.deepcopy(case)
        assert len(stripped['rows']) == 56
        for row in stripped['rows']:
            for g in row['geometry']:
                after = g.pop('post_bake_particles')
                time = g.pop('native_time_after_bake')
                count = g.pop('particle_count_after_bake')
                fixed = g.pop('fixed_delta_time')
                delta = g.pop('delta_time')
                assert count == row['count'] == len(after)
                assert time == row['native_system_time']
                if after != row['particles']:
                    changed += 1
                clocks.append(dict(node_path=case['node_path'], seed=row['seed'],
                    requested_time=row['requested_time'], repetition=row['repetition'],
                    fixture=g['fixture'], fixed_delta_time=fixed, delta_time=delta,
                    live_particle_snapshot_changed=after != row['particles']))
                observations += 1
                vertices += g['vertices']
        assert stripped == original, 'Original geometry or particle reference changed'
    assert observations == 336
    probe.job.check(prepared['input_pins'])
    probe.save('BAKE-STATE-MEASUREMENTS.v1.json', dict(observations=clocks,
        observations_measured=observations, live_snapshot_change_observations=changed,
        interpolation_or_color_cause_qualified=False, gpu_rendered=False, full_import=False))
    return dict(geometry_observations_verified=observations, vertex_observations=vertices,
        prior_336_geometry_observations_unchanged=True,
        prior_112_particle_frames_unchanged=True, live_snapshot_change_observations=changed,
        native_bake_state_probe_verified=True)

def main():
    prior = probe.read(OLD / 'RUN-RESULT.v1.json')
    assert prior['native_geometry_verified'] and prior['unity_exit_code'] == 0
    assert probe.sha(OLD / 'INDEPENDENT-REOPEN.v1.json') == prior['independent_receipt_sha256']
    prior_proof = probe.read(OLD / 'INDEPENDENT-REOPEN.v1.json')
    probe.job.check(prior_proof['input_pins'])
    assert probe.sha(OLD / 'GEOMETRY.v1.json') == prior_proof['capture_sha256']
    source = BASE.read_text('utf-8-sig')
    source = source[:source.index("if __name__=='__main__':")]
    # Reuse only the existing preparation/run functions. Pin every helper body
    # and the original geometry before spawning the new controller.
    import ast
    tree = ast.parse(source)
    functions = [n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name in {'prepare', 'run'}]
    code = ast.unparse(ast.Module(body=functions, type_ignores=[]))
    code = code.replace('OpenRPGFirePitParticleGeometry', 'OpenRPGFirePitBakeStateProbe')
    code = code.replace('OpenRPGFirePitParticleGeometry.cs', 'OpenRPGFirePitBakeStateProbe.cs')
    # AST formatting is deterministic; add pins directly before job.check.
    anchor = 'job.check(pins)'
    assert code.count(anchor) == 1
    code = code.replace(anchor, "\n    for extra in [BASE, HERE/'queue_delta_firepit_effect_materials_v1.py', HERE/'queue_delta_firepit_particle_behavior_v4.py', OLD/'GEOMETRY.v1.json', OLD/'INDEPENDENT-REOPEN.v1.json', OLD/'RUN-RESULT.v1.json']:\n        pins[str(extra)]=sha(extra)\n    job.check(pins)")
    code = code.replace('capturing_336_native_billboard_and_atlas_observations', 'capturing_live_particle_snapshots_after_336_native_bakes')
    code = code.replace('336_native_billboard_and_atlas_observations_reopened', '336_native_bakes_and_post_bake_live_particle_snapshots_reopened')
    namespace = dict(probe.__dict__)
    namespace.update(__file__=str(Path(__file__).resolve()), BASE=BASE, OLD=OLD,
        helper_source=helper_source, verify=verify)
    exec(compile(code, str(Path(__file__).resolve()), 'exec'), namespace)
    namespace['run' if len(sys.argv) > 1 and sys.argv[1] == 'run' else 'prepare']()

if __name__ == '__main__':
    main()
