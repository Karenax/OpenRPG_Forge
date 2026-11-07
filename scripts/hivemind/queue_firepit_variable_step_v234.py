"""Measure both native BakeMesh transform modes without altering existing captures."""
from pathlib import Path
import ast, copy, importlib.util, json, sys

HERE=Path(__file__).parent
BASE=HERE/'queue_firepit_particle_geometry_v1.py'
spec=importlib.util.spec_from_file_location('geometry_probe_v228',BASE)
probe=importlib.util.module_from_spec(spec);spec.loader.exec_module(probe)
OLD=probe.W/'unity-delta-firepit-particle-geometry-v1'
probe.ROOT=probe.W/'firepit-particle-variable-step-native-v234';probe.job.ROOT=probe.ROOT
original_helper=probe.helper_source
BENCH_DRIFT=[]

def refresh_private_bench(pins):
    # Old receipts pin the entire mutable bench, including unrelated static/Pirate
    # materials migrated since the particle acquisition. Keep those receipts intact
    # and pin the present bench for a new experiment; require identical old particle
    # states and geometry in verify(). Never rebase original source files or helpers.
    allowed={'M_FirePit_A_01.mat','M_FoodSeed_01a.mat','M_Cannon02.mat','M_FoodKit_01a.mat',
             'M_WoodTrim_01.mat','M_FishnMeat_01a.mat','M_Cannon.mat','M_WoodCarvingTrim_01b.mat',
             'M_PirateHat.mat','M_BottleKit_01a.mat','M_MetalKitPots_01a.mat','M_Wood_Planks_01.mat',
             'M_WoodBarrel_01a.mat','M_RocksTemperate_01.mat','M_EuropeanBeech_Detail_01.mat','VFXManager.asset'}
    refreshed=dict(pins)
    for name,expected in pins.items():
        p=Path(name)
        if p.is_relative_to(probe.PROJECT) and p.name in allowed:
            actual=probe.sha(p)
            if actual!=expected:
                assert p.suffix=='.mat' or p==probe.PROJECT/'ProjectSettings/VFXManager.asset'
                BENCH_DRIFT.append(dict(path=name,historical_sha256=expected,current_sha256=actual,
                    effect_material_or_prefab=False,old_receipt_preserved=True))
                refreshed[name]=actual
    assert len(BENCH_DRIFT)==17,'Unexpected private bench drift; inspect before proceeding'
    return refreshed

def helper_source():
    text=original_helper().replace('OpenRPGFirePitParticleGeometry','OpenRPGFirePitParticleVariableStepV234')
    assert text.count('ps.Simulate(time,false,true,true);')==1
    text=text.replace('ps.Simulate(time,false,true,true);','ps.Simulate(time,false,true,false);').replace('fixedTimeStep=true','fixedTimeStep=false; controlled variable-step comparison')
    anchor='[Serializable] public class Geometry {';assert text.count(anchor)==1
    text=text.replace(anchor,anchor+'public Vector3[] with_transform_positions,with_transform_normals;public Vector4[] with_transform_tangents;public Color32[] with_transform_colors;public int[] with_transform_indices;public Matrix4x4 renderer_local_to_world;')
    anchor='renderer.BakeMesh(mesh,camera,false);';assert text.count(anchor)==1
    text=text.replace(anchor,anchor+'''
    Vector3[] withPositions,withNormals;Vector4[] withTangents;Color32[] withColors;int[] withIndices;
    var transformedMesh=new Mesh();transformedMesh.hideFlags=HideFlags.HideAndDontSave;
    try{renderer.BakeMesh(transformedMesh,camera,true);withPositions=transformedMesh.vertices;withNormals=transformedMesh.normals;withTangents=transformedMesh.tangents;withColors=transformedMesh.colors32;withIndices=transformedMesh.triangles;}
    finally{UnityEngine.Object.DestroyImmediate(transformedMesh);}
''')
    anchor='rows.Add(new Geometry{fixture=fixture,';assert text.count(anchor)==1
    text=text.replace(anchor,'rows.Add(new Geometry{with_transform_positions=withPositions,with_transform_normals=withNormals,with_transform_tangents=withTangents,with_transform_colors=withColors,with_transform_indices=withIndices,renderer_local_to_world=renderer.localToWorldMatrix,fixture=fixture,')
    return text

def verify(report,prepared):
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

def main():
    prior=probe.read(OLD/'RUN-RESULT.v1.json');assert prior['native_geometry_verified'] and prior['unity_exit_code']==0
    assert probe.sha(OLD/'INDEPENDENT-REOPEN.v1.json')==prior['independent_receipt_sha256']
    source=BASE.read_text('utf-8-sig');tree=ast.parse(source)
    functions=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'prepare','run'}]
    code=ast.unparse(ast.Module(body=functions,type_ignores=[])).replace('OpenRPGFirePitParticleGeometry','OpenRPGFirePitParticleVariableStepV234')
    assert code.count("job.check(proof['input_pins'])")==1
    code=code.replace("job.check(proof['input_pins'])","refreshed_prior_pins=refresh_private_bench(proof['input_pins'])\n    job.check(refreshed_prior_pins)")
    assert code.count("pins.update(proof['input_pins'])")==1
    code=code.replace("pins.update(proof['input_pins'])","pins.update(refreshed_prior_pins)")
    anchor='job.check(pins)';assert code.count(anchor)==1
    code=code.replace(anchor,"save('PRIVATE-BENCH-DRIFT.v1.json',dict(status='new_experiment_pins_current_private_bench',rows=BENCH_DRIFT,source_files_rebased=False,previous_particle_reference_unchanged=True,full_import=False))\n    for extra in [BASE, HERE/'queue_firepit_variable_step_v234.py', OLD/'GEOMETRY.v1.json', OLD/'INDEPENDENT-REOPEN.v1.json', OLD/'RUN-RESULT.v1.json', ROOT/'PRIVATE-BENCH-DRIFT.v1.json']:\n        pins[str(extra)]=sha(extra)\n    job.check(pins)")
    scope=probe.__dict__;scope.update(__file__=str(Path(__file__).resolve()),helper_source=helper_source,verify=verify,BASE=BASE,OLD=OLD,refresh_private_bench=refresh_private_bench,BENCH_DRIFT=BENCH_DRIFT)
    exec(compile(code,str(BASE),'exec'),scope)
    if len(sys.argv)>1:
        assert sys.argv[1:]==['run'];scope['run']()
    else:scope['prepare']()

if __name__=='__main__':main()
