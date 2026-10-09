"""Measure source-gradient lookup hypotheses against every native Color32 quad.

The first native example suggests a quantized lifetime lookup, not just a clock
offset. Keep prior failures. No particle remapping or runtime correction.
"""
from pathlib import Path
import datetime
import hashlib
import json
import math
import struct

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-firepit-gradient-lookup-v1'
SOURCE = W / 'unity-delta-firepit-particle-geometry-v1/GEOMETRY.v1.json'
UPSTREAM = W / 'delta-firepit-geometry-buffers-v2/RESULT.v1.json'

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return json.loads(p.read_text('utf-8-sig'))
def check(pins):
    for name, digest in pins.items(): assert sha(Path(name)) == digest, 'Changed native input: ' + name
def f32(value): return struct.unpack('<f', struct.pack('<f', value))[0]
def linear(value): return f32(value/12.92 if value <= .04045 else ((value+.055)/1.055)**2.4)

def evaluate(keys, time):
    if time <= keys[0][0]: return keys[0][1]
    if time >= keys[-1][0]: return keys[-1][1]
    for (a_time, a), (b_time, b) in zip(keys, keys[1:]):
        if a_time <= time <= b_time:
            weight = f32(f32(time-a_time)/f32(b_time-a_time))
            return f32(a+f32(f32(b-a)*weight))
    raise AssertionError('Native gradient time span absent')

def main():
    assert not ROOT.exists(), 'Keep previous measurement'
    proof = read(UPSTREAM); assert proof['observations_verified'] == 336
    pins = dict(proof['input_pins'])
    for p in [SOURCE, UPSTREAM, Path(__file__).resolve(),
        W/'delta-firepit-vertex-color-hypotheses-v1/RESULT.v1.json',
        W/'delta-firepit-simulation-residual-v1/RESULT.v1.json']: pins[str(p)] = sha(p)
    check(pins); capture = read(SOURCE); ROOT.mkdir()
    grids = ['continuous', 'floor_64', 'floor_128', 'floor_256', 'floor_128_div127', 'floor_127_div127']
    transfers = ['raw', 'linear_after_gradient', 'linear_before_gradient']
    quantizers = ['nearest', 'truncate']
    clocks = ['native_particle_age', 'add_render_residual', 'subtract_render_residual']
    protocol = dict(hypotheses_derived_from_initial_native_example=True, lifetime_grids=grids,
        color_transfers=transfers, quantizers=quantizers, clocks=clocks,
        channel_tolerance_color32_bytes=1, exact_matches_reported_separately=True,
        no_runtime_correction_installed=True, full_import=False)
    (ROOT/'PREDECLARED.v1.json').write_text(json.dumps(protocol, indent=2), 'utf-8')
    pins[str(ROOT/'PREDECLARED.v1.json')] = sha(ROOT/'PREDECLARED.v1.json')
    results = {}; observations = 0; samples = 0
    for case in capture['cases']:
        system = json.loads(case['native_particle_json'])['ParticleSystem']
        module = system['ColorModule']; assert module['enabled'] == 1
        spec = module['gradient']; assert spec['minMaxState'] == 1
        gradient = spec['maxGradient']; assert gradient['m_Mode'] == 0
        tables = {}
        for transfer in transfers:
            tables[transfer] = {}
            for channel in 'rgba':
                count = gradient['m_NumAlphaKeys' if channel == 'a' else 'm_NumColorKeys']
                times = 'atime' if channel == 'a' else 'ctime'
                keys = [(f32(gradient[times+str(i)]/65535),
                    f32(gradient['key'+str(i)][channel])) for i in range(count)]
                if transfer == 'linear_before_gradient' and channel != 'a':
                    keys = [(t, linear(v)) for t, v in keys]
                assert all(b[0] > a[0] for a,b in zip(keys,keys[1:]))
                tables[transfer][channel] = keys
        for row in case['rows']:
            phase = math.fmod(row['requested_time']*case['original_simulation_speed'], case['original_duration'])
            residual = phase-row['native_system_time']
            if residual < 0: residual += case['original_duration']
            assert 0 <= residual < .021
            for geometry in row['geometry']:
                assert geometry['vertices'] == row['count']*4
                observations += 1
                for index, particle in enumerate(row['particles']):
                    assert all(particle['start_color'][k] == 255 for k in 'rgba'), 'Do not invent start-color multiplication'
                    colors = geometry['colors'][index*4:index*4+4]
                    assert len(colors) == 4 and all(c == colors[0] for c in colors)
                    actual = colors[0]; samples += 1
                    for clock in clocks:
                        delta = 0 if clock == 'native_particle_age' else residual*(1 if clock == 'add_render_residual' else -1)
                        age = max(0.0, min(1.0, f32(1-f32(f32(particle['remaining_lifetime']-delta)/particle['start_lifetime']))))
                        for grid in grids:
                            time = age
                            if grid.startswith('floor_'):
                                terms = grid.split('_')[1:]; scale = int(terms[0])
                                divisor = int(terms[1][3:]) if len(terms) == 2 else scale
                                time = max(0.0, min(1.0, f32(math.floor(age*scale)/divisor)))
                            for transfer in transfers:
                                values = {k:evaluate(tables[transfer][k],time) for k in 'rgba'}
                                if transfer == 'linear_after_gradient':
                                    values.update({k:linear(values[k]) for k in 'rgb'})
                                for quantizer in quantizers:
                                    name = '|'.join([clock,grid,transfer,quantizer])
                                    state = results.setdefault(name, dict(maximum_byte_error=0,
                                        exact_quads=0, within_one_byte_quads=0, samples=0, by_node={}))
                                    predicted = {k:min(255,max(0,math.floor(f32(values[k]*255)+(0.5 if quantizer=='nearest' else 0)))) for k in 'rgba'}
                                    error = max(abs(predicted[k]-actual[k]) for k in 'rgba')
                                    state['samples'] += 1; state['maximum_byte_error'] = max(state['maximum_byte_error'],error)
                                    state['exact_quads'] += int(error==0); state['within_one_byte_quads'] += int(error<=1)
                                    node = state['by_node'].setdefault(case['node_path'],dict(samples=0, maximum_byte_error=0,exact_quads=0,within_one_byte_quads=0))
                                    node['samples'] += 1; node['maximum_byte_error'] = max(node['maximum_byte_error'],error)
                                    node['exact_quads'] += int(error==0); node['within_one_byte_quads'] += int(error<=1)
    assert observations == 336 and samples == 7536
    check(pins)
    exact = [name for name, state in results.items() if state['maximum_byte_error']==0]
    one_byte = [name for name, state in results.items() if state['maximum_byte_error']<=1]
    ranked = sorted(results, key=lambda name:(-results[name]['within_one_byte_quads'],results[name]['maximum_byte_error'],-results[name]['exact_quads']))
    result = dict(status='native_source_gradient_lookup_hypotheses_measured', input_pins=pins,
        observations_verified=observations, quad_color_samples=samples, hypotheses=results,
        exact_matching_hypotheses=exact, within_one_byte_matching_hypotheses=one_byte,
        ranked_hypotheses=ranked, unique_exact_mapping_qualified=len(exact)==1,
        native_internal_lookup_cause_qualified=False, autonomous_runtime_qualified=False,
        no_runtime_correction_installed=True, full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(observations_verified=observations,quad_color_samples=samples,
        exact_matching_hypotheses=exact,within_one_byte_matching_hypotheses=one_byte,
        best=[dict(name=name,**results[name]) for name in ranked[:3]],full_import=False)))

if __name__ == '__main__': main()
