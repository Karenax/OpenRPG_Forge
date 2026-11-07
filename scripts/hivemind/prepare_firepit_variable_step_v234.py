"""Version a native control: change only Simulate fixedTimeStep in isolated clones."""
from pathlib import Path
import ast,json,subprocess,sys
S=Path(__file__).parent
old=S/'queue_firepit_transform_probe_v230.py';new=S/'queue_firepit_variable_step_v234.py'
assert not new.exists()
text=old.read_text('utf-8').replace('firepit-particle-transform-native-v230','firepit-particle-variable-step-native-v234').replace('OpenRPGFirePitParticleTransformProbeV230','OpenRPGFirePitParticleVariableStepV234').replace('queue_firepit_transform_probe_v230.py','queue_firepit_variable_step_v234.py')
anchor="    text=original_helper().replace('OpenRPGFirePitParticleGeometry','OpenRPGFirePitParticleVariableStepV234')"
assert text.count(anchor)==1
text=text.replace(anchor,anchor+"\n    assert text.count('ps.Simulate(time,false,true,true);')==1\n    text=text.replace('ps.Simulate(time,false,true,true);','ps.Simulate(time,false,true,false);').replace('fixedTimeStep=true','fixedTimeStep=false; controlled variable-step comparison')")
start=text.index('def verify(');end=text.index('\ndef main():',start)
replacement='''def verify(report,prepared):
    old=probe.read(OLD/'GEOMETRY.v1.json');observations=0;vertices=0;pairs={}
    assert len(report['cases'])==len(old['cases'])==2
    for case,reference in zip(report['cases'],old['cases']):
        for key in reference:
            if key not in ['rows','simulation_protocol']:assert case[key]==reference[key],key
        assert 'fixedTimeStep=false' in case['simulation_protocol']
        assert len(case['rows'])==len(reference['rows'])==56
        for row,baseline in zip(case['rows'],reference['rows']):
            for key in ['seed','requested_time','repetition','local_to_world','native_simulation_space','native_scaling_mode']:assert row[key]==baseline[key],key
            assert row['count']==len(row['particles'])
            for g in row['geometry']:
                assert g['vertices']==4*row['count'] and len(g['indices'])==6*row['count']
                assert g['with_transform_indices']==g['indices'] and g['with_transform_colors']==g['colors']
                assert len(g['with_transform_positions'])==g['vertices']
                assert g['renderer_local_to_world']==g['particle_local_to_world']==row['local_to_world']
                key=(case['node_path'],row['seed'],row['requested_time'],g['fixture'])
                payload=dict(particles=row['particles'],native_system_time=row['native_system_time'],geometry=g)
                if key in pairs:assert pairs[key]==payload,'Variable-step repeated native capture differs'
                else:pairs[key]=payload
                observations+=1;vertices+=g['vertices']
    assert observations==336;probe.job.check(prepared['input_pins'])
    probe.save('TRANSFORM-MEASUREMENTS.v1.json',dict(status='controlled_native_variable_step_particle_geometry_capture',geometry_observations=observations,vertices=vertices,original_component_configuration_unchanged=True,only_clone_simulation_protocol_changed=True,repeats_identical=True,source_original_runtime_qualified=False,full_import=False))
    return dict(geometry_observations_verified=336,vertex_observations=vertices,variable_step_control=True,prior_capture_preserved=True,original_component_configuration_unchanged=True,native_repeats_exact=True,both_native_transform_modes_captured=True)
'''
text=text[:start]+replacement+text[end:]
compile(text,str(new),'exec');new.write_text(text,'utf-8')
subprocess.run([sys.executable,'-B',str(new)],check=True)
