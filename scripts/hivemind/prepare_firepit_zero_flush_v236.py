"""Native control for pending renderer state after fixed-step Simulate."""
from pathlib import Path
import subprocess,sys
S=Path(__file__).parent;old=S/'queue_firepit_transform_probe_v230.py';new=S/'queue_firepit_zero_flush_v236.py'
assert not new.exists()
text=old.read_text('utf-8').replace('firepit-particle-transform-native-v230','firepit-particle-zero-flush-native-v236').replace('OpenRPGFirePitParticleTransformProbeV230','OpenRPGFirePitParticleZeroFlushV236').replace('queue_firepit_transform_probe_v230.py','queue_firepit_zero_flush_v236.py')
anchor="    text=original_helper().replace('OpenRPGFirePitParticleGeometry','OpenRPGFirePitParticleZeroFlushV236')";assert text.count(anchor)==1
extra='''
    anchor='[Serializable] public class Frame {';assert text.count(anchor)==1
    text=text.replace(anchor,anchor+'public bool zero_flush_particle_structs_equal,zero_flush_system_time_bits_equal;public float before_flush_system_time;')
    anchor='ps.Simulate(time,false,true,true);';assert text.count(anchor)==1
    text=text.replace(anchor,anchor+"var preFlushTime=ps.time;var preFlushParticles=new ParticleSystem.Particle[ps.main.maxParticles];int preFlushCount=ps.GetParticles(preFlushParticles);ps.Simulate(0f,false,false,false);")
    anchor='int count=ps.GetParticles(buffer);';assert text.count(anchor)==1
    text=text.replace(anchor,anchor+"bool flushEqual=count==preFlushCount;for(int q=0;q<Math.Min(count,preFlushCount);q++){flushEqual&=preFlushParticles[q].Equals(buffer[q]);}bool flushClockEqual=BitConverter.SingleToInt32Bits(preFlushTime)==BitConverter.SingleToInt32Bits(ps.time);")
    anchor='rows.Add(new Frame{geometry=BakeGeometry(ps),';assert text.count(anchor)==1
    text=text.replace(anchor,anchor+'zero_flush_particle_structs_equal=flushEqual,zero_flush_system_time_bits_equal=flushClockEqual,before_flush_system_time=preFlushTime,')
    text=text.replace('fixedTimeStep=true','fixedTimeStep=true; controlled subsequent Simulate(0,withChildren=false,restart=false,fixedTimeStep=false)')
'''
text=text.replace(anchor,anchor+extra)
start=text.index('def verify(');end=text.index('\ndef main():',start)
replacement='''def verify(report,prepared):
    old=probe.read(OLD/'GEOMETRY.v1.json');observations=0;vertices=0;pairs={};preserved_particles=preserved_clocks=0
    assert len(report['cases'])==len(old['cases'])==2
    for case,reference in zip(report['cases'],old['cases']):
        for key in reference:
            if key not in ['rows','simulation_protocol']:assert case[key]==reference[key],key
        assert 'controlled subsequent Simulate(0' in case['simulation_protocol']
        assert len(case['rows'])==len(reference['rows'])==56
        for row,baseline in zip(case['rows'],reference['rows']):
            for key in ['seed','requested_time','repetition','local_to_world','native_simulation_space','native_scaling_mode']:assert row[key]==baseline[key],key
            assert row['before_flush_system_time']==baseline['native_system_time']
            preserved_particles+=int(row['zero_flush_particle_structs_equal']);preserved_clocks+=int(row['zero_flush_system_time_bits_equal'])
            assert row['count']==len(row['particles'])
            for g in row['geometry']:
                assert g['vertices']==4*row['count'] and len(g['indices'])==6*row['count']
                assert g['with_transform_indices']==g['indices'] and g['with_transform_colors']==g['colors']
                key=(case['node_path'],row['seed'],row['requested_time'],g['fixture'])
                payload=dict(particles=row['particles'],native_system_time=row['native_system_time'],geometry=g)
                if key in pairs:assert pairs[key]==payload,'Repeated native zero-flush capture differs'
                else:pairs[key]=payload
                observations+=1;vertices+=g['vertices']
    assert observations==336;probe.job.check(prepared['input_pins'])
    probe.save('TRANSFORM-MEASUREMENTS.v1.json',dict(status='controlled_native_zero_duration_flush_capture',geometry_observations=observations,vertices=vertices,particle_structs_preserved_frames=preserved_particles,system_time_bits_preserved_frames=preserved_clocks,total_frames=112,only_clone_simulation_protocol_changed=True,repeats_identical=True,source_original_runtime_qualified=False,full_import=False))
    return dict(geometry_observations_verified=336,vertex_observations=vertices,zero_duration_flush_control=True,particle_structs_preserved_frames=preserved_particles,system_time_bits_preserved_frames=preserved_clocks,prior_capture_preserved=True,original_component_configuration_unchanged=True,native_repeats_exact=True,both_native_transform_modes_captured=True)
'''
text=text[:start]+replacement+text[end:];compile(text,str(new),'exec');new.write_text(text,'utf-8')
subprocess.run([sys.executable,'-B',str(new)],check=True)
